using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Neko.AbstractionLayer;
using Neko.EntityComponentSystem;
using Neko.Extensions.Logging;
using Neko.Globals;
using Neko.Math;
using Neko.Vulkan;

using Vortice.Vulkan;

namespace Neko.Rendering.Renderer3D;

sealed class IndirectData {
  public readonly List<VkDrawIndexedIndirectCommand> Commands = [];
  public int VisibleCount;
  public uint CurrentIndexOffset;
}

readonly struct CmdRef(uint pool, int cmdIndex) {
  public readonly uint Pool = pool;
  public readonly int CmdIndex = cmdIndex;
}

public partial class Render3DSystem : SystemBase, IRenderSystem {
  public const string Simple3D = "simple3D";
  public const string Skinned3D = "skinned3D";

  public const string HatchTextureName = "./Resources/T_crossHatching13_D.png";
  public static float HatchScale = 1;

  public int LastElemRenderedCount => NotSkinnedNodesCache.Length + SkinnedNodesCache.Length;
  public int LastKnownElemCount { get; private set; } = 0;
  public ulong LastKnownElemSize { get; private set; } = 0;
  public ulong LastKnownSkinnedElemCount { get; private set; }
  public ulong LastKnownSkinnedElemJointsCount { get; private set; }

  private NekoBuffer[] _indirectSimpleBuffers = [];
  private NekoBuffer[] _indirectComplexBuffers = [];

  private readonly IDescriptorSetLayout _jointDescriptorLayout = null!;
  private List<VkDrawIndexedIndirectCommand> _indirectDrawCommands = [];
  private Dictionary<uint, IndirectData> _indirectSimples = [];
  private Dictionary<uint, IndirectData> _indirectComplexes = [];
  private uint _instanceIndexSimple = 0;
  private uint _instanceIndexComplex = 0;

  public Node[] NotSkinnedNodesCache { get; private set; } = [];
  public Node[] SkinnedNodesCache { get; private set; } = [];
  private Node[] _flatNodesCache = [];
  private IRender3DElement[] _renderablesCache = [];

  private ITexture _hatchTexture = null!;
  private readonly IDescriptorSetLayout _previousTexturesLayout = null!;

  private BufferPool _simpleBufferPool = null!;
  private BufferPool _complexBufferPool = null!;
  private readonly List<VertexBinding> _vertexSimpleBindings = [];
  private readonly List<VertexBinding> _vertexComplexBindings = [];
  private bool _cacheMatch = false;

  private Node[] _simpleBufferNodes = [];
  private Node[] _complexBufferNodes = [];

  // private readonly Dictionary<string, float> _texIndexCache = new(StringComparer.Ordinal);
  private readonly Dictionary<Guid, float> _texIndexCache = new();

  private ObjectData[] _objectDataScratch = [];
  private Matrix4x4[] _jointsScratch = [];
  private int _totalJointCount = 0;
  private readonly Dictionary<Node, int> _nodeToObjIndex = [];
  private readonly Dictionary<Node, int> _nodeToJointsOffset = [];

  private readonly Dictionary<Node, CmdRef> _simpleCmdMap = [];
  private readonly Dictionary<Node, CmdRef> _complexCmdMap = [];

  private List<Node> _visSimple = [];
  private List<Node> _visComplex = [];

  private readonly Dictionary<uint, List<VkDrawIndexedIndirectCommand>> _simpleVisibleScratch = [];
  private readonly Dictionary<uint, List<VkDrawIndexedIndirectCommand>> _complexVisibleScratch = [];

  private readonly IDescriptorSetLayout[] _basicLayouts = [];

  public Render3DSystem(
    Application app,
    nint allocator,
    VulkanDevice device,
    IRenderer renderer,
    TextureManager textureManager,
    Dictionary<string, IDescriptorSetLayout> externalLayouts,
    IPipelineConfigInfo configInfo = null!
  ) : base(app, allocator, device, renderer, textureManager, configInfo) {
    _jointDescriptorLayout = new VulkanDescriptorSetLayout.Builder(_device)
      .AddBinding(0, DescriptorType.UniformBuffer, ShaderStageFlags.AllGraphics)
      .Build();

    _basicLayouts = [
      _textureManager.AllTexturesSetLayout,
      externalLayouts["Global"],
      externalLayouts["ObjectData"],
      externalLayouts["PointLight"],
    ];

    IDescriptorSetLayout[] complexLayouts = [
      .. _basicLayouts,
      externalLayouts["JointsBuffer"],
    ];

    AddPipelineData(new() {
      RenderPass = renderer.GetSwapchainRenderPass(),
      VertexName = "model_vertex",
      FragmentName = "model_fragment",
      GeometryName = "model_geometry",
      PipelineProvider = new PipelineModelProvider(),
      DescriptorSetLayouts = [.. _basicLayouts],
      PipelineName = Simple3D
    });

    AddPipelineData(new() {
      RenderPass = renderer.GetSwapchainRenderPass(),
      VertexName = "model_skinned_vertex",
      FragmentName = "model_skinned_fragment",
      GeometryName = "model_geometry",
      PipelineProvider = new PipelineModelProvider(),
      DescriptorSetLayouts = [.. complexLayouts],
      PipelineName = Skinned3D
    });

    _descriptorPool = new VulkanDescriptorPool.Builder((VulkanDevice)_device)
      .SetMaxSets(CommonConstants.MAX_SETS)
      .AddPoolSize(DescriptorType.UniformBuffer, CommonConstants.MAX_SETS)
      .AddPoolSize(DescriptorType.SampledImage, CommonConstants.MAX_SETS)
      .AddPoolSize(DescriptorType.Sampler, CommonConstants.MAX_SETS)
      .AddPoolSize(DescriptorType.InputAttachment, CommonConstants.MAX_SETS)
      .AddPoolSize(DescriptorType.StorageBuffer, CommonConstants.MAX_SETS)
      .SetPoolFlags(DescriptorPoolCreateFlags.UpdateAfterBind)
      .Build();

    _simpleBufferPool = new(_device, _allocator);
    _complexBufferPool = new(_device, _allocator);

    Logger.Info("[RENDER 3D SYSTEM] Constructor");
  }

  public void Setup(ReadOnlySpan<IRender3DElement> renderables, ref TextureManager textures) {
    if (renderables.Length < 1) {
      Logger.Warn("Entities that are capable of using 3D renderer are less than 1, thus 3D Render System won't be recreated");
      return;
    }

    LastKnownElemCount = CalculateNodesLength(renderables);
    LastKnownElemSize = 0;
    var (len, joints) = CalculateNodesLengthWithSkin(renderables);
    LastKnownSkinnedElemCount = (ulong)len;
    LastKnownSkinnedElemJointsCount = (ulong)joints;

    Logger.Info($"Recreating Renderer 3D [{LastKnownSkinnedElemCount}]");

    for (int i = 0; i < renderables.Length; i++) {
      LastKnownElemSize += renderables[i]!.CalculateBufferSize();
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveOptimization)]
  public void Update(FrameInfo frameInfo, ConcurrentDictionary<Guid, Mesh> meshes) {
    if (_objectDataScratch.Length == 0) return;

    for (int i = 0; i < NotSkinnedNodesCache.Length; i++) {
      var node = NotSkinnedNodesCache[i];
      var owner = node.ParentRenderer.Owner;
      if (owner.CanBeDisposed) continue;

      var transform = owner.GetTransform();
      var mesh = meshes[node.MeshGuid];

      int idx = _nodeToObjIndex[node];
      ref var od = ref _objectDataScratch[idx];

      od.ModelMatrix = transform?.Matrix() ?? Matrix4x4.Identity;
      od.NormalMatrix = transform?.NormalMatrix() ?? Matrix4x4.Identity;
      od.NodeMatrix = mesh.Matrix;
      od.JointsBufferOffset = Vector4.Zero;
    }

    for (int i = 0; i < SkinnedNodesCache.Length; i++) {
      var node = SkinnedNodesCache[i];
      var owner = node.ParentRenderer.Owner;
      if (owner.CanBeDisposed) continue;

      var transform = owner.GetTransform();
      var mesh = meshes[node.MeshGuid];

      int objIdx = _nodeToObjIndex[node];
      int jointsOffset = _nodeToJointsOffset[node];

      ref var od = ref _objectDataScratch[objIdx];

      od.ModelMatrix = transform?.Matrix() ?? Matrix4x4.Identity;
      od.NormalMatrix = transform?.NormalMatrix() ?? Matrix4x4.Identity;
      od.NodeMatrix = mesh.Matrix;
      od.JointsBufferOffset = new Vector4(jointsOffset, 0, 0, 0);

      var skin = _application.Skins[node.SkinGuid];
      var joints = skin.OutputNodeMatrices;
      Array.Copy(joints, 0, _jointsScratch, jointsOffset, joints.Length);
    }

    unsafe {
      fixed (ObjectData* pObjectData = _objectDataScratch) {
        _application.StorageCollection.WriteBuffer(
          "ObjectStorage",
          frameInfo.FrameIndex,
          (nint)pObjectData,
          (ulong)Unsafe.SizeOf<ObjectData>() * (ulong)_objectDataScratch.Length
        );
      }
      fixed (Matrix4x4* pMatrices = _jointsScratch) {
        _application.StorageCollection.WriteBuffer(
          "JointsStorage",
          frameInfo.FrameIndex,
          (nint)pMatrices,
          (ulong)Unsafe.SizeOf<Matrix4x4>() * (ulong)_totalJointCount
        );
      }
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveOptimization)]
  public void Render(
    IRender3DElement[] renderables,
    ConcurrentDictionary<Guid, Mesh> meshes,
    FrameInfo frameInfo,
    out IEnumerable<Node> animatedNodes,
    bool indirect = true
  ) {
    PerfMonitor.Clear3DRendererInfo();
    PerfMonitor.NumberOfObjectsRenderedIn3DRenderer = (uint)LastElemRenderedCount;
    CreateOrUpdateBuffers(renderables, meshes);
    uint visible = RefillIndirectBuffersWithCulling(meshes, out animatedNodes);
    PerfMonitor.Clear3DRendererInfo();
    PerfMonitor.NumberOfObjectsRenderedIn3DRenderer = visible;

    if (indirect) {
      if (_simpleBufferNodes.Length > 0) {
        RenderSimpleIndirect(frameInfo);
      }
      if (_complexBufferNodes.Length > 0) {
        RenderComplexIndirect(frameInfo);
      }
    } else {
      if (_simpleBufferNodes.Length > 0) {
        RenderSimple(frameInfo, _simpleBufferNodes, meshes);
      }
      if (_complexBufferNodes.Length > 0) {
        RenderComplex(frameInfo, _complexBufferNodes, meshes, _simpleBufferNodes.Length);
      }
    }
  }

  public unsafe void RenderSimple(
    FrameInfo frameInfo,
    Span<Node> nodes,
    ConcurrentDictionary<Guid, Mesh> meshes
  ) {
    BindPipeline(frameInfo.CommandBuffer, Simple3D);

    Descriptor.BindDescriptorSet(_device, _textureManager.AllTexturesDescriptor, frameInfo, _pipelines[Simple3D].PipelineLayout, 0, 1);
    Descriptor.BindDescriptorSet(_device, frameInfo.GlobalDescriptorSet, frameInfo, _pipelines[Simple3D].PipelineLayout, 1, 1);
    Descriptor.BindDescriptorSet(_device, frameInfo.ObjectDataDescriptorSet, frameInfo, _pipelines[Simple3D].PipelineLayout, 2, 1);
    Descriptor.BindDescriptorSet(_device, frameInfo.PointLightsDescriptorSet, frameInfo, _pipelines[Simple3D].PipelineLayout, 3, 1);

    uint lastBuffer = uint.MaxValue;
    uint indexOffset = 0;

    // _renderer.CommandList.BindIndex(frameInfo.CommandBuffer, _globalSimpleIndexBuffer!, 0);

    for (int i = 0; i < nodes.Length; i++) {
      if (nodes[i].ParentRenderer.Owner.CanBeDisposed || !nodes[i].ParentRenderer.Owner.Active) continue;
      if (!nodes[i].ParentRenderer.FinishedInitialization) continue;

      uint thisCount = (uint)meshes[nodes[i].MeshGuid].Indices.Length;
      var bindInfo = _vertexSimpleBindings[i];

      var vertexBuffer = _simpleBufferPool.GetVertexBuffer(bindInfo.BufferIndex);
      var indexBuffer = _simpleBufferPool.GetIndexBuffer(bindInfo.BufferIndex);

      _renderer.CommandList.BindVertex(frameInfo.CommandBuffer, vertexBuffer, 0);
      _renderer.CommandList.BindIndex(frameInfo.CommandBuffer, indexBuffer!, 0);

      // Bind the mesh’s index buffer and draw
      // nodes[i].BindNode(frameInfo.CommandBuffer);
      // nodes[i].DrawNode(frameInfo.CommandBuffer, (int)bindInfo.FirstVertexOffset, (uint)i);

      _renderer.CommandList.DrawIndexed(
        frameInfo.CommandBuffer,
        indexCount: thisCount,
        instanceCount: 1,
        firstIndex: bindInfo.FirstIndexOffset,
        vertexOffset: (int)bindInfo.FirstVertexOffset,
        firstInstance: (uint)i
      );

      indexOffset += thisCount;
    }
  }

  public unsafe void RenderComplex(
    FrameInfo frameInfo,
    Span<Node> nodes,
    ConcurrentDictionary<Guid, Mesh> meshes,
    int offset
  ) {
    BindPipeline(frameInfo.CommandBuffer, Skinned3D);

    Descriptor.BindDescriptorSet(_device, _textureManager.AllTexturesDescriptor, frameInfo, _pipelines[Skinned3D].PipelineLayout, 0, 1);
    Descriptor.BindDescriptorSet(_device, frameInfo.GlobalDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 1, 1);
    Descriptor.BindDescriptorSet(_device, frameInfo.ObjectDataDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 2, 1);
    Descriptor.BindDescriptorSet(_device, frameInfo.PointLightsDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 3, 1);
    Descriptor.BindDescriptorSet(_device, frameInfo.JointsBufferDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 4, 1);

    uint lastBuffer = uint.MaxValue;
    uint indexOffset = 0;

    // _renderer.CommandList.BindIndex(frameInfo.CommandBuffer, _globalComplexIndexBuffer!, 0);

    for (int i = 0; i < nodes.Length; i++) {
      uint thisCount = (uint)meshes[nodes[i].MeshGuid].Indices.Length;

      if (!nodes[i].ParentRenderer.Owner.Active || nodes[i].ParentRenderer.Owner.CanBeDisposed || meshes[nodes[i].MeshGuid]?.IndexCount < 1) {
        indexOffset += thisCount;
        continue;
      }

      var bindInfo = _vertexComplexBindings[i];

      if (bindInfo.BufferIndex != lastBuffer) {
        lastBuffer = bindInfo.BufferIndex;
      }

      var vertexBuffer = _complexBufferPool.GetVertexBuffer(bindInfo.BufferIndex);
      var indexBuffer = _complexBufferPool.GetIndexBuffer(bindInfo.BufferIndex);

      _renderer.CommandList.BindIndex(frameInfo.CommandBuffer, indexBuffer, 0);
      _renderer.CommandList.BindVertex(frameInfo.CommandBuffer, vertexBuffer, 0);

      _renderer.CommandList.DrawIndexed(
        frameInfo.CommandBuffer,
        indexCount: thisCount,
        instanceCount: 1,
        firstIndex: bindInfo.FirstIndexOffset,
        vertexOffset: (int)bindInfo.FirstVertexOffset,
        firstInstance: (uint)i + (uint)offset
      );

      indexOffset += thisCount;
    }
  }

  public unsafe void RenderSimpleIndirect(FrameInfo frameInfo) {
    BindPipeline(frameInfo.CommandBuffer, Simple3D);

    Descriptor.BindDescriptorSet(_device, _textureManager.AllTexturesDescriptor, frameInfo, _pipelines[Simple3D].PipelineLayout, 0, 1);
    Descriptor.BindDescriptorSet(_device, frameInfo.GlobalDescriptorSet, frameInfo, _pipelines[Simple3D].PipelineLayout, 1, 1);
    Descriptor.BindDescriptorSet(_device, frameInfo.ObjectDataDescriptorSet, frameInfo, _pipelines[Simple3D].PipelineLayout, 2, 1);
    Descriptor.BindDescriptorSet(_device, frameInfo.PointLightsDescriptorSet, frameInfo, _pipelines[Simple3D].PipelineLayout, 3, 1);

    // _renderer.CommandList.BindIndex(frameInfo.CommandBuffer, _globalSimpleIndexBuffer!, 0);
    // _renderer.CommandList.BindIndex(frameInfo.CommandBuffer, _globalSimpleIndexPool!.GetIndexBuffer(0), 0);

    foreach (var container in _indirectSimples) {
      var targetIndex = _simpleBufferPool.GetIndexBuffer(container.Key);
      var targetVertex = _simpleBufferPool.GetVertexBuffer(container.Key);

      _renderer.CommandList.BindIndex(frameInfo.CommandBuffer, targetIndex, 0);
      _renderer.CommandList.BindVertex(frameInfo.CommandBuffer, targetVertex, 0);

      _renderer.CommandList.DrawIndexedIndirect(
        frameInfo.CommandBuffer,
        _indirectSimpleBuffers[container.Key]!.GetBuffer(),
        0,
        (uint)container.Value.VisibleCount,
        (uint)Unsafe.SizeOf<VkDrawIndexedIndirectCommand>()
      );
    }
  }

  public unsafe void RenderComplexIndirect(FrameInfo frameInfo) {
    BindPipeline(frameInfo.CommandBuffer, Skinned3D);

    Descriptor.BindDescriptorSet(_device, _textureManager.AllTexturesDescriptor, frameInfo, _pipelines[Skinned3D].PipelineLayout, 0, 1);
    Descriptor.BindDescriptorSet(_device, frameInfo.GlobalDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 1, 1);
    Descriptor.BindDescriptorSet(_device, frameInfo.ObjectDataDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 2, 1);
    Descriptor.BindDescriptorSet(_device, frameInfo.PointLightsDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 3, 1);
    Descriptor.BindDescriptorSet(_device, frameInfo.JointsBufferDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 4, 1);

    // _renderer.CommandList.BindIndex(frameInfo.CommandBuffer, _globalComplexIndexBuffer!, 0);

    foreach (var container in _indirectComplexes) {
      var targetIndex = _complexBufferPool.GetIndexBuffer(container.Key);
      var targetVertex = _complexBufferPool.GetVertexBuffer(container.Key);

      _renderer.CommandList.BindIndex(frameInfo.CommandBuffer, targetIndex, 0);
      _renderer.CommandList.BindVertex(frameInfo.CommandBuffer, targetVertex, 0);

      _renderer.CommandList.DrawIndexedIndirect(
        frameInfo.CommandBuffer,
        _indirectComplexBuffers[container.Key]!.GetBuffer(),
        0,
        (uint)container.Value.VisibleCount,                    // CHANGED
        (uint)Unsafe.SizeOf<VkDrawIndexedIndirectCommand>()
      );
    }
  }

  public bool CheckTextures(ReadOnlySpan<IRender3DElement> renderables) {
    var len = CalculateLengthOfPool(renderables);
    // return len == _texturesCount;

    return true;
  }

  public bool CheckSizes(ReadOnlySpan<IRender3DElement> renderables) {
    var newCount = CalculateNodesLength(renderables);

    if (newCount > LastKnownElemCount) {
      return false;
    }

    return true;
  }

  private static int CalculateLengthOfPool(ReadOnlySpan<IRender3DElement> renderables) {
    int count = 0;
    for (int i = 0; i < renderables.Length; i++) {
      count += renderables[i].MeshedNodesCount;
    }
    return count;
  }

  private static int CalculateNodesLength(ReadOnlySpan<IRender3DElement> renderables) {
    int len = 0;
    foreach (var renderable in renderables) {
      len += renderable.MeshedNodesCount;
    }
    return len;
  }
  private static int GetIndexOfMyTexture(string texName) {
    var texturePair = Application.Instance.TextureManager.PerSceneLoadedTextures.Where(x => x.Value.TextureName == texName).Single();
    return texturePair.Value.TextureManagerIndex;
  }

  private (int len, int joints) CalculateNodesLengthWithSkin(ReadOnlySpan<IRender3DElement> renderables) {
    int len = 0;
    int joints = 0;
    foreach (var renderable in renderables) {
      foreach (var mNode in renderable.MeshedNodes) {
        if (mNode.HasSkin) {
          len += 1;
          var skin = _application.Skins[mNode.SkinGuid];
          joints += skin.OutputNodeMatrices.Length;
        }
      }
    }
    return (len, joints);
  }

  private bool CacheMatch(in IRender3DElement[] drawables) {
    var cachedSimpleBatchIndices = NotSkinnedNodesCache
      .Select(x => x.BatchId)
      .ToArray();
    var cachedSkinnedBatchIndices = SkinnedNodesCache
      .Select(x => x.BatchId)
      .ToArray();
    Guid[] cachedBatchIndices = [.. cachedSimpleBatchIndices, .. cachedSkinnedBatchIndices];

    List<Guid> newBatchIndices = [];
    foreach (var drawable in drawables) {
      newBatchIndices.AddRange([.. drawable.MeshedNodes.Select(x => x.BatchId)]);
    }

    if (cachedBatchIndices.SequenceEqual(newBatchIndices)) {
      return true;
    }

    return false;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  private float GetOrAddTextureIndex(Guid textureId) {
    ref float texId = ref CollectionsMarshal.GetValueRefOrAddDefault(
      _texIndexCache,
      textureId,
      out var exists
    );

    if (!exists) {
      var targetTexture = _textureManager.GetTextureLocal(textureId);
      texId = GetIndexOfMyTexture(targetTexture.TextureName);
    }

    return texId;
  }

  private static unsafe void CopyCmdListToBuffer(List<VkDrawIndexedIndirectCommand> list, NekoBuffer buf) {
    var span = CollectionsMarshal.AsSpan(list);
    if (span.Length == 0) return;

    ref var first = ref MemoryMarshal.GetReference(span);
    fixed (VkDrawIndexedIndirectCommand* p = &first) {
      ulong bytes = (ulong)span.Length * (ulong)Unsafe.SizeOf<VkDrawIndexedIndirectCommand>();
      buf.WriteToBuffer((nint)p, bytes, 0);
      buf.Flush(bytes, 0);
    }
  }

  private void EnsureVisibleScratchCapacity() {
    foreach (var (pool, data) in _indirectSimples) {
      if (!_simpleVisibleScratch.TryGetValue(pool, out var list))
        _simpleVisibleScratch[pool] = list = new List<VkDrawIndexedIndirectCommand>(data.Commands.Count);
      else if (list.Capacity < data.Commands.Count)
        list.Capacity = data.Commands.Count;
    }

    foreach (var (pool, data) in _indirectComplexes) {
      if (!_complexVisibleScratch.TryGetValue(pool, out var list))
        _complexVisibleScratch[pool] = list = new List<VkDrawIndexedIndirectCommand>(data.Commands.Count);
      else if (list.Capacity < data.Commands.Count)
        list.Capacity = data.Commands.Count;
    }
  }

  private unsafe uint RefillIndirectBuffersWithCulling(ConcurrentDictionary<Guid, Mesh> meshes, out IEnumerable<Node> animatedNodes) {
    // clear scratch
    foreach (var s in _simpleVisibleScratch.Values) s.Clear();
    foreach (var s in _complexVisibleScratch.Values) s.Clear();

    var planes = Frustum.BuildFromCamera(CameraState.GetCamera());

    // --- SIMPLE (non-skinned) ---
    _visSimple.Clear();
    Frustum.FilterNodesByFog(_simpleBufferNodes, out _visSimple); // your culling
    // Frustum.FilterNodesByPlanes(in planes, [.. _simpleBufferNodes], out visSimpleIn);

    foreach (var n in _visSimple) {
      var owner = n.ParentRenderer.Owner;
      if (owner.CanBeDisposed || !owner.Active) continue;
      if (meshes[n.MeshGuid]?.IndexCount < 1) continue;

      if (!_simpleCmdMap.TryGetValue(n, out var r)) continue;
      var src = _indirectSimples[r.Pool].Commands[r.CmdIndex];

      if (!_simpleVisibleScratch.TryGetValue(r.Pool, out var list)) {
        list = new List<VkDrawIndexedIndirectCommand>(32);
        _simpleVisibleScratch[r.Pool] = list;
      }
      list.Add(src);
    }

    // write packed commands to buffers (front of buffer)
    foreach (var kv in _simpleVisibleScratch) {
      var pool = kv.Key;
      var list = kv.Value;
      _indirectSimples[pool].VisibleCount = list.Count;
      CopyCmdListToBuffer(list, _indirectSimpleBuffers[pool]);
    }

    // --- COMPLEX (skinned) ---
    _visComplex.Clear();
    Frustum.FilterNodesByFog(new List<Node>(_complexBufferNodes), out _visComplex);
    animatedNodes = _visComplex;
    // Frustum.FilterNodesByPlanes(in planes, [.. _complexBufferNodes], out visComplexIn);

    foreach (var n in _visComplex) {
      var owner = n.ParentRenderer.Owner;
      if (owner.CanBeDisposed || !owner.Active) continue;
      if (meshes[n.MeshGuid]?.IndexCount < 1) continue;

      if (!_complexCmdMap.TryGetValue(n, out var r)) continue;
      var src = _indirectComplexes[r.Pool].Commands[r.CmdIndex];

      if (!_complexVisibleScratch.TryGetValue(r.Pool, out var list)) {
        list = new List<VkDrawIndexedIndirectCommand>(32);
        _complexVisibleScratch[r.Pool] = list;
      }
      list.Add(src);
    }

    foreach (var kv in _complexVisibleScratch) {
      var pool = kv.Key;
      var list = kv.Value;
      _indirectComplexes[pool].VisibleCount = list.Count;
      CopyCmdListToBuffer(list, _indirectComplexBuffers[pool]);
    }

    uint total = 0;
    foreach (var d in _indirectSimples.Values) total += (uint)d.VisibleCount;
    foreach (var d in _indirectComplexes.Values) total += (uint)d.VisibleCount;
    return total;
  }

  private void CreateOrUpdateBuffers(IRender3DElement[] renderables, ConcurrentDictionary<Guid, Mesh> meshes) {
    // short-circuit if renderables identical and pools already present
    _cacheMatch = _renderablesCache.SequenceEqual(renderables);
    if (_cacheMatch && _complexBufferPool != null && _simpleBufferPool != null)
      return;

    _instanceIndexSimple = 0;
    _instanceIndexComplex = 0;

    // flatten once (no need to do it in Update anymore)
    Frustum.FlattenNodes(renderables, out var flatNodes);
    _flatNodesCache = [.. flatNodes];

    // split/skinned ordering + deterministic sorting
    var nodeObjectsSkinned = new List<Node>(flatNodes.Count / 2);
    var nodeObjectsNotSkinned = new List<Node>(flatNodes.Count / 2);

    foreach (var node in flatNodes) {
      if (!node.Enabled || !node.HasMesh) continue;
      if (node.HasSkin) nodeObjectsSkinned.Add(node);
      else nodeObjectsNotSkinned.Add(node);
    }

    nodeObjectsSkinned.Sort(static (a, b) => a.CompareTo(b));
    nodeObjectsNotSkinned.Sort(static (a, b) => a.CompareTo(b));

    NotSkinnedNodesCache = nodeObjectsNotSkinned.ToArray();
    SkinnedNodesCache = nodeObjectsSkinned.ToArray();

    // allocate once: object data array (non-skinned first, then skinned)
    int notCount = NotSkinnedNodesCache.Length;
    int skinCount = SkinnedNodesCache.Length;
    int totalObjs = notCount + skinCount;

    _objectDataScratch = (totalObjs == 0) ? Array.Empty<ObjectData>() : new ObjectData[totalObjs];

    // build index map for fast writes in Update()
    _nodeToObjIndex.Clear();
    for (int i = 0; i < notCount; i++)
      _nodeToObjIndex[NotSkinnedNodesCache[i]] = i;
    for (int i = 0; i < skinCount; i++)
      _nodeToObjIndex[SkinnedNodesCache[i]] = notCount + i;

    // build joints layout (stable offsets) and allocate once
    _nodeToJointsOffset.Clear();
    int jointsOffset = 0;
    for (int i = 0; i < skinCount; i++) {
      var n = SkinnedNodesCache[i];
      var skin = _application.Skins[n.SkinGuid];
      int len = skin.OutputNodeMatrices.Length;
      _nodeToJointsOffset[n] = jointsOffset;
      jointsOffset += len;
    }
    _totalJointCount = jointsOffset;
    _jointsScratch = (_totalJointCount == 0) ? Array.Empty<Matrix4x4>() : new Matrix4x4[_totalJointCount];

    // reset/trim texture id cache if topology changed a lot
    _texIndexCache.Clear();

    // --- GPU buffers that depend on topology/order only ---
    Logger.Info("[RENDER 3D] Recreating Buffers");
    if (notCount > 0) {
      // CreateSimpleVertexBuffer(NotSkinnedNodesCache, meshes);
      // CreateSimpleIndexBuffer(NotSkinnedNodesCache, meshes);
      CreateSimpleVertexIndexBuffer(NotSkinnedNodesCache, meshes);
    }
    if (skinCount > 0) {
      // CreateComplexVertexBuffer(SkinnedNodesCache, meshes);
      // CreateComplexIndexBuffer(SkinnedNodesCache, meshes);
      CreateComplexVertexIndexBuffer(SkinnedNodesCache, meshes);
    }

    EnsureIndirectBuffers(_indirectSimples, ref _indirectSimpleBuffers);
    EnsureIndirectBuffers(_indirectComplexes, ref _indirectComplexBuffers);

    _renderablesCache = (IRender3DElement[])renderables.Clone();
    _cacheMatch = true;

    for (int i = 0; i < NotSkinnedNodesCache.Length; i++) {
      var node = NotSkinnedNodesCache[i];
      var owner = node.ParentRenderer.Owner;
      var material = owner.GetMaterial();
      var mesh = meshes[node.MeshGuid];

      float texId = GetOrAddTextureIndex(mesh.TextureIdReference);

      ref var od = ref _objectDataScratch[i];
      // only static fields here; matrices are updated per-frame
      od.ColorAndFilterFlag = new Vector4(material?.Color ?? Vector3.Zero, node.FilterMeInShader ? 1f : 0f);
      od.AmbientAndTexId0 = new Vector4(material?.Ambient ?? Vector3.Zero, texId);
      od.DiffuseAndTexId1 = new Vector4(material?.Diffuse ?? Vector3.Zero, texId);
      od.SpecularAndShininess = new Vector4(material?.Specular ?? Vector3.Zero, material?.Shininess ?? 0.0f);
      od.JointsBufferOffset = Vector4.Zero;
    }

    for (int i = 0; i < SkinnedNodesCache.Length; i++) {
      var node = SkinnedNodesCache[i];
      var owner = node.ParentRenderer.Owner;
      var material = owner.GetMaterial();
      var mesh = meshes[node.MeshGuid];

      float texId = GetOrAddTextureIndex(mesh.TextureIdReference);

      int objIdx = _nodeToObjIndex[node];

      ref var od = ref _objectDataScratch[objIdx];
      od.ColorAndFilterFlag = new Vector4(material?.Color ?? Vector3.Zero, node.FilterMeInShader ? 1f : 0f);
      od.AmbientAndTexId0 = new Vector4(material?.Ambient ?? Vector3.Zero, texId);
      od.DiffuseAndTexId1 = new Vector4(material?.Diffuse ?? Vector3.Zero, texId);
      od.SpecularAndShininess = new Vector4(material?.Specular ?? Vector3.Zero, material?.Shininess ?? 0.0f);

      if (_nodeToJointsOffset.TryGetValue(node, out var joff))
        od.JointsBufferOffset = new Vector4(joff, 0, 0, 0);
    }

    EnsureVisibleScratchCapacity();
  }

  private static int AddIndirectCommand(
    ConcurrentDictionary<Guid, Mesh> meshes,
    uint index,
    Node node,
    VertexBinding vertexBinding,
    ref Dictionary<uint, IndirectData> pair,
    ref uint instanceIndex,
    in uint additionalIndexOffset = 0
  ) {
    if (!pair.TryGetValue(index, out var data)) {
      data = new IndirectData();
      pair[index] = data;
    }

    var mesh = meshes[node.MeshGuid] ?? throw new NullReferenceException("mesh is null");
    if (mesh.IndexCount < 1) throw new ArgumentNullException(nameof(meshes), "mesh does not have indices");

    var cmd = new VkDrawIndexedIndirectCommand {
      indexCount = (uint)mesh.Indices.Length,
      instanceCount = 1,
      firstIndex = vertexBinding.FirstIndexOffset,
      vertexOffset = (int)vertexBinding.FirstVertexOffset,
      firstInstance = instanceIndex + additionalIndexOffset
    };

    data.Commands.Add(cmd);
    int cmdIdx = data.Commands.Count - 1;

    instanceIndex++;
    data.CurrentIndexOffset += vertexBinding.FirstIndexOffset;

    return cmdIdx;
  }

  private unsafe void EnsureIndirectBuffers(
  Dictionary<uint, IndirectData> pair,
  ref NekoBuffer[] buffArray
) {
    // Make sure the array can index directly by 'pool key'
    int maxKey = pair.Count == 0 ? -1 : (int)pair.Keys.Max();
    if (maxKey < 0) { buffArray = []; return; }

    if (buffArray == null || buffArray.Length <= maxKey) {
      // grow but DO NOT touch existing buffers
      var newArr = new NekoBuffer[maxKey + 1];
      if (buffArray != null) Array.Copy(buffArray, newArr, buffArray.Length);
      buffArray = newArr;
    }

    foreach (var kv in pair) {
      uint pool = kv.Key;
      var data = kv.Value;
      ulong neededBytes = (ulong)data.Commands.Count * (ulong)Unsafe.SizeOf<VkDrawIndexedIndirectCommand>();

      var existing = buffArray[pool];
      bool needsAlloc = existing == null || existing.GetBufferSize() < neededBytes;

      if (!needsAlloc) continue;

      existing?.Dispose();

      var inBuff = new NekoBuffer(
        _allocator,
        _device,
        neededBytes,
        BufferUsage.IndirectBuffer,
        MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
        stagingBuffer: false,
        cpuAccessible: true
      );

      inBuff.Map(neededBytes);

      buffArray[pool] = inBuff;

      var span = CollectionsMarshal.AsSpan(data.Commands);
      if (span.Length > 0) {
        ref var first = ref MemoryMarshal.GetReference(span);
        fixed (VkDrawIndexedIndirectCommand* p = &first) {
          inBuff.WriteToBuffer((nint)p, neededBytes, 0);
          inBuff.Flush(neededBytes, 0);
        }
      }
    }
  }
  private static bool CheckForOverflow(uint a, uint b) {
    if (a > uint.MaxValue - b) {
      return true;
    }
    return false;
  }

  private unsafe void CreateComplexVertexIndexBuffer(
    ReadOnlySpan<Node> nodes,
    ConcurrentDictionary<Guid, Mesh> meshes
  ) {
    _vertexComplexBindings.Clear();
    _complexBufferPool.Dispose();
    _complexBufferPool = new BufferPool(_device, _allocator);
    _indirectComplexes.Clear();

    var currentPool = 0u;
    var indexOffset = 0u;
    var vertexOffset = 0u;

    var adjustedIndices = new List<uint>();
    var previousVertexSize = 0ul;
    var previousIndexSize = 0ul;

    _complexBufferNodes = nodes.ToArray();
    foreach (var node in nodes) {
      ReadOnlySpan<Vertex> verts = meshes[node.MeshGuid].Vertices;
      ReadOnlySpan<uint> indices = meshes[node.MeshGuid].Indices;

      var vertexByteSize = (ulong)verts.Length * (ulong)Unsafe.SizeOf<Vertex>();
      var indexByteSize = (ulong)indices.Length * sizeof(uint);

      ulong vertexByteOffset;
      var canAddVertex = _complexBufferPool.CanAddToVertexBuffer(
        index: currentPool,
        byteSize: vertexByteSize,
        prevSize: previousVertexSize
      );
      var canAddIndex = _complexBufferPool.CanAddToIndexBuffer(
        index: currentPool,
        byteSize: indexByteSize,
        prevSize: previousIndexSize
      );

      var localIndices = new List<uint>();
      foreach (var idx in meshes[node.MeshGuid].Indices) {
        adjustedIndices.Add(idx + vertexOffset);
        localIndices.Add(idx + vertexOffset);
      }
      var localIndicesByteSize = (ulong)localIndices.Count * sizeof(uint);

      var isOverflowingOnNextStep = CheckForOverflow(indexOffset, (uint)indices.Length);
      // Logger.Info($"[{canAddIndex}][{canAddVertex}][{isOverflowingOnNextStep}]");
      if (!canAddVertex || !canAddIndex || isOverflowingOnNextStep) {
        Logger.Info($"Adding to pool [{canAddVertex}][{canAddIndex}][{isOverflowingOnNextStep}]");
        currentPool = (uint)_complexBufferPool.AddToPool();
        previousVertexSize = 0;
        previousIndexSize = 0;
        indexOffset = 0;
        vertexOffset = 0;
      }
      fixed (Vertex* pVertex = verts) {
        _complexBufferPool.AddToBuffer(currentPool, (nint)pVertex, vertexByteSize, previousVertexSize, out vertexByteOffset, out var reason);
      }

      fixed (uint* pIndicesToPool = localIndices.ToArray()) {
        _complexBufferPool.AddToIndex(
          currentPool,
          (nint)pIndicesToPool,
          localIndicesByteSize,
          previousIndexSize,
          out var indexByteOffset,
          out var idxReason
        );
      }

      previousVertexSize += vertexByteSize;
      previousIndexSize += localIndicesByteSize;

      _vertexComplexBindings.Add(new VertexBinding {
        BufferIndex = currentPool,
        FirstVertexOffset = (uint)(vertexByteOffset / (ulong)Unsafe.SizeOf<Vertex>()),
        FirstIndexOffset = indexOffset
      });

      int cmdIdx = AddIndirectCommand(
        meshes,
        currentPool,
        node,
        _vertexComplexBindings.Last(),
        ref _indirectComplexes,
        ref _instanceIndexComplex,
        in _instanceIndexSimple
      );

      _complexCmdMap[node] = new CmdRef(currentPool, cmdIdx);

      indexOffset += (uint)indices.Length;
      vertexOffset += (uint)meshes[node.MeshGuid].Vertices.Length;
    }
  }

  private unsafe void CreateSimpleVertexIndexBuffer(
    ReadOnlySpan<Node> nodes,
    ConcurrentDictionary<Guid, Mesh> meshes
  ) {
    _vertexSimpleBindings.Clear();
    _simpleBufferPool.Dispose();
    _simpleBufferPool = new BufferPool(_device, _allocator);
    _indirectSimples.Clear();

    var currentPool = 0u;
    var indexOffset = 0u;
    var vertexOffset = 0u;

    var adjustedIndices = new List<uint>();
    var previousVertexSize = 0ul;
    var previousIndexSize = 0ul;

    _simpleBufferNodes = nodes.ToArray();
    foreach (var node in nodes) {
      ReadOnlySpan<Vertex> verts = meshes[node.MeshGuid].Vertices;
      ReadOnlySpan<uint> indices = meshes[node.MeshGuid].Indices;

      var vertexByteSize = (ulong)verts.Length * (ulong)Unsafe.SizeOf<Vertex>();
      var indexByteSize = (ulong)indices.Length * sizeof(uint);

      ulong vertexByteOffset;
      var canAddVertex = _simpleBufferPool.CanAddToVertexBuffer(
        index: currentPool,
        byteSize: vertexByteSize,
        prevSize: previousVertexSize
      );
      var canAddIndex = _simpleBufferPool.CanAddToIndexBuffer(
        index: currentPool,
        byteSize: indexByteSize,
        prevSize: previousIndexSize
      );

      var localIndices = new List<uint>();
      foreach (var idx in meshes[node.MeshGuid].Indices) {
        adjustedIndices.Add(idx + vertexOffset);
        localIndices.Add(idx + vertexOffset);
      }
      var localIndicesByteSize = (ulong)localIndices.Count * sizeof(uint);

      var isOverflowingOnNextStep = CheckForOverflow(indexOffset, (uint)indices.Length);
      // Logger.Info($"[{canAddIndex}][{canAddVertex}][{isOverflowingOnNextStep}]");
      if (!canAddVertex || !canAddIndex || isOverflowingOnNextStep) {
        Logger.Info($"Adding to pool [{canAddVertex}][{canAddIndex}][{isOverflowingOnNextStep}]");
        currentPool = (uint)_simpleBufferPool.AddToPool();
        previousVertexSize = 0;
        previousIndexSize = 0;
        indexOffset = 0;
        vertexOffset = 0;
      }
      fixed (Vertex* pVertex = verts) {
        _simpleBufferPool.AddToBuffer(currentPool, (nint)pVertex, vertexByteSize, previousVertexSize, out vertexByteOffset, out var reason);
      }

      fixed (uint* pIndicesToPool = localIndices.ToArray()) {
        _simpleBufferPool.AddToIndex(
          currentPool,
          (nint)pIndicesToPool,
          localIndicesByteSize,
          previousIndexSize,
          out var indexByteOffset,
          out var idxReason
        );
      }

      previousVertexSize += vertexByteSize;
      previousIndexSize += localIndicesByteSize;

      _vertexSimpleBindings.Add(new VertexBinding {
        BufferIndex = currentPool,
        FirstVertexOffset = (uint)(vertexByteOffset / (ulong)Unsafe.SizeOf<Vertex>()),
        FirstIndexOffset = indexOffset
      });

      int cmdIdx = AddIndirectCommand(
        meshes,
        currentPool,
        node,
        _vertexSimpleBindings.Last(),
        ref _indirectSimples,
        ref _instanceIndexSimple
      );
      _simpleCmdMap[node] = new CmdRef(currentPool, cmdIdx);

      indexOffset += (uint)indices.Length;
      vertexOffset += (uint)meshes[node.MeshGuid].Vertices.Length;
    }
  }

  private unsafe void CreateSimpleVertexIndexBuffer_Clean_Ref(ReadOnlySpan<Node> nodes, ConcurrentDictionary<Guid, Mesh> meshes) {
    _vertexSimpleBindings.Clear();
    _simpleBufferPool.Dispose();
    _simpleBufferPool = new BufferPool(_device, _allocator);
    _indirectSimples.Clear();
    _indirectSimples = [];
    var currentPool = 0u;
    var indexOffset = 0u;
    var vertexOffset = 0u;

    var adjustedIndices = new List<uint>();
    var previousVertexSize = 0ul;

    _simpleBufferNodes = nodes.ToArray();
    foreach (var node in nodes) {
      ReadOnlySpan<Vertex> verts = meshes[node.MeshGuid].Vertices;
      ReadOnlySpan<uint> indices = meshes[node.MeshGuid].Indices;

      var vertexByteSize = (ulong)verts.Length * (ulong)Unsafe.SizeOf<Vertex>();
      var indexByteSize = (ulong)indices.Length * sizeof(uint);

      ulong vertexByteOffset;
      var canAddVertex = _simpleBufferPool.CanAddToVertexBuffer(currentPool, vertexByteSize, previousVertexSize);
      if (!canAddVertex) {
        currentPool = (uint)_simpleBufferPool.AddToPool();
        previousVertexSize = 0;
      }
      fixed (Vertex* pVertex = verts) {
        _simpleBufferPool.AddToBuffer(currentPool, (nint)pVertex, vertexByteSize, previousVertexSize, out vertexByteOffset, out var reason);
      }

      foreach (var idx in meshes[node.MeshGuid].Indices) {
        adjustedIndices.Add(idx + vertexOffset);
      }

      previousVertexSize += vertexByteSize;

      _vertexSimpleBindings.Add(new VertexBinding {
        BufferIndex = currentPool,
        FirstVertexOffset = (uint)(vertexByteOffset / (ulong)Unsafe.SizeOf<Vertex>()),
        FirstIndexOffset = indexOffset
      });

      int cmdIdx = AddIndirectCommand(
        meshes,
        currentPool,
        node,
        _vertexSimpleBindings.Last(),
        ref _indirectSimples,
        ref _instanceIndexSimple
      );
      _simpleCmdMap[node] = new CmdRef(currentPool, cmdIdx);

      indexOffset += (uint)indices.Length;
      vertexOffset += (uint)meshes[node.MeshGuid].Vertices.Length;
    }

    var adjustedIndicesByteSize = (ulong)adjustedIndices.Count * sizeof(uint);
    var indexStagingBuffer = new NekoBuffer(
      _allocator,
      _device,
      adjustedIndicesByteSize,
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
      stagingBuffer: true,
      cpuAccessible: true
    );

    indexStagingBuffer.Map(adjustedIndicesByteSize);
    fixed (uint* pIndices = adjustedIndices.ToArray()) {
      indexStagingBuffer.WriteToBuffer((nint)pIndices, adjustedIndicesByteSize);
    }
    indexStagingBuffer.Unmap();

    // _globalSimpleIndexBuffer = new NekoBuffer(
    //   _allocator,
    //   _device,
    //   (ulong)Unsafe.SizeOf<uint>(),
    //   (uint)adjustedIndices.Count,
    //   BufferUsage.IndexBuffer | BufferUsage.TransferDst,
    //   MemoryProperty.DeviceLocal
    // );

    // _device.CopyBuffer(
    //   indexStagingBuffer.GetBuffer(),
    //   _globalSimpleIndexBuffer.GetBuffer(),
    //   adjustedIndicesByteSize
    // );
    indexStagingBuffer.Dispose();
  }

  public override unsafe void Dispose() {
    _device.WaitQueue();
    foreach (var buff in _indirectSimpleBuffers) {
      buff.Dispose();
    }
    foreach (var buff in _indirectComplexBuffers) {
      buff.Dispose();
    }
    _vertexComplexBindings.Clear();
    _vertexSimpleBindings.Clear();
    _complexBufferPool?.Dispose();
    _simpleBufferPool?.Dispose();
    _jointDescriptorLayout?.Dispose();
    LastKnownElemCount = 0;
    LastKnownElemSize = 0;
    LastKnownSkinnedElemCount = 0;
    LastKnownSkinnedElemJointsCount = 0;
    _instanceIndexSimple = 0;
    _instanceIndexComplex = 0;
    SkinnedNodesCache = [];
    NotSkinnedNodesCache = [];
    _cacheMatch = false;
    _indirectSimples.Clear();
    _indirectComplexes.Clear();
    base.Dispose();
  }
}