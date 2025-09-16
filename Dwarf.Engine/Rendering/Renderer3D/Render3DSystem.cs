using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Dwarf.AbstractionLayer;
using Dwarf.EntityComponentSystem;
using Dwarf.Extensions.Logging;
using Dwarf.Globals;
using Dwarf.Math;
using Dwarf.Rendering.Renderer3D.Animations;
using Dwarf.Utils;
using Dwarf.Vulkan;

using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Rendering.Renderer3D;

// NEW: holds the full template commands + the current visible count
sealed class IndirectData {
  public readonly List<VkDrawIndexedIndirectCommand> Commands = new();
  public int VisibleCount;               // how many are visible this frame
  public uint CurrentIndexOffset;        // (your existing bookkeeping)
}

// NEW: stable mapping from Node -> (pool, cmdIndex in the template)
readonly struct CmdRef {
  public readonly uint Pool;
  public readonly int CmdIndex;
  public CmdRef(uint pool, int cmdIndex) { Pool = pool; CmdIndex = cmdIndex; }
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

  private DwarfBuffer? _globalSimpleIndexBuffer;
  private DwarfBuffer? _globalComplexIndexBuffer;

  private DwarfBuffer[] _indirectSimpleBuffers = [];
  private DwarfBuffer[] _indirectComplexBuffers = [];

  private readonly IDescriptorSetLayout _jointDescriptorLayout = null!;
  private List<VkDrawIndexedIndirectCommand> _indirectDrawCommands = [];
  private Dictionary<uint, IndirectData> _indirectSimples = [];
  private Dictionary<uint, IndirectData> _indirectComplexes = [];
  private uint _instanceIndexSimple = 0;
  private uint _instanceIndexComplex = 0;

  public Node[] NotSkinnedNodesCache { get; private set; } = [];
  public Node[] SkinnedNodesCache { get; private set; } = [];
  private ObjectData[] _objectDataCache = [];
  private Node[] _flatNodesCache = [];
  private IRender3DElement[] _renderablesCache = [];

  private List<(string, int)> _skinnedGroups = [];
  private List<(string, int)> _notSkinnedGroups = [];

  private ITexture _hatchTexture = null!;
  private IDescriptorSetLayout _previousTexturesLayout = null!;

  private BufferPool _simpleBufferPool = null!;
  private BufferPool _complexBufferPool = null!;
  private List<VertexBinding> _vertexSimpleBindings = [];
  private List<VertexBinding> _vertexComplexBindings = [];
  private bool _cacheMatch = false;

  private Node[] _simpleBufferNodes = [];
  private Node[] _complexBufferNodes = [];


  private readonly Dictionary<string, float> _texIndexCache = new(StringComparer.Ordinal);
  private ObjectData[] _objectDataScratch = Array.Empty<ObjectData>();
  private Matrix4x4[] _jointsScratch = Array.Empty<Matrix4x4>();
  private int _totalJointCount = 0;
  private readonly Dictionary<Node, int> _nodeToObjIndex = new();
  private readonly Dictionary<Node, int> _nodeToJointsOffset = new();

  // NEWz

  private readonly Dictionary<Node, CmdRef> _simpleCmdMap = new();
  private readonly Dictionary<Node, CmdRef> _complexCmdMap = new();

  private readonly Dictionary<uint, List<VkDrawIndexedIndirectCommand>> _simpleVisibleScratch = new();
  private readonly Dictionary<uint, List<VkDrawIndexedIndirectCommand>> _complexVisibleScratch = new();

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

    IDescriptorSetLayout[] basicLayouts = [
      _textureManager.AllTexturesSetLayout,
      externalLayouts["Global"],
      externalLayouts["ObjectData"],
      externalLayouts["PointLight"],
    ];

    IDescriptorSetLayout[] complexLayouts = [
      .. basicLayouts,
      externalLayouts["JointsBuffer"],
    ];

    AddPipelineData(new() {
      RenderPass = renderer.GetSwapchainRenderPass(),
      VertexName = "model_vertex",
      FragmentName = "model_fragment",
      GeometryName = "model_geometry",
      PipelineProvider = new PipelineModelProvider(),
      DescriptorSetLayouts = [.. basicLayouts],
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

    _renderer.CommandList.BindIndex(frameInfo.CommandBuffer, _globalSimpleIndexBuffer!, 0);

    for (int i = 0; i < nodes.Length; i++) {
      if (nodes[i].ParentRenderer.Owner.CanBeDisposed || !nodes[i].ParentRenderer.Owner.Active) continue;
      if (!nodes[i].ParentRenderer.FinishedInitialization) continue;

      uint thisCount = (uint)meshes[nodes[i].MeshGuid].Indices.Length;
      var bindInfo = _vertexSimpleBindings[i];

      var buffer = _simpleBufferPool.GetVertexBuffer(bindInfo.BufferIndex);
      _renderer.CommandList.BindVertex(frameInfo.CommandBuffer, buffer, 0);
      // _renderer.CommandList.BindIndex(frameInfo.CommandBuffer, _globalIndexBuffer!, 0);

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

    _renderer.CommandList.BindIndex(frameInfo.CommandBuffer, _globalComplexIndexBuffer!, 0);

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

    _renderer.CommandList.BindIndex(frameInfo.CommandBuffer, _globalSimpleIndexBuffer!, 0);

    foreach (var container in _indirectSimples) {
      var targetVertex = _simpleBufferPool.GetVertexBuffer(container.Key);
      _renderer.CommandList.BindVertex(frameInfo.CommandBuffer, targetVertex, 0);

      _renderer.CommandList.DrawIndexedIndirect(
        frameInfo.CommandBuffer,
        _indirectSimpleBuffers[container.Key]!.GetBuffer(),
        0,
        (uint)container.Value.VisibleCount,                    // CHANGED
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

    _renderer.CommandList.BindIndex(frameInfo.CommandBuffer, _globalComplexIndexBuffer!, 0);

    foreach (var container in _indirectComplexes) {
      var targetVertex = _complexBufferPool.GetVertexBuffer(container.Key);
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

  public unsafe void RenderIndirect(FrameInfo frameInfo) {
    BindPipeline(frameInfo.CommandBuffer, Skinned3D);

    Descriptor.BindDescriptorSet(_device, _textureManager.AllTexturesDescriptor, frameInfo, _pipelines[Skinned3D].PipelineLayout, 0, 1);
    Descriptor.BindDescriptorSet(_device, frameInfo.GlobalDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 1, 1);
    Descriptor.BindDescriptorSet(_device, frameInfo.ObjectDataDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 2, 1);
    Descriptor.BindDescriptorSet(_device, frameInfo.PointLightsDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 3, 1);
    Descriptor.BindDescriptorSet(_device, frameInfo.JointsBufferDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 4, 1);

    _renderer.CommandList.BindIndex(frameInfo.CommandBuffer, _globalComplexIndexBuffer!, 0);

    foreach (var container in _indirectComplexes) {
      var targetVertex = _complexBufferPool.GetVertexBuffer(container.Key);
      _renderer.CommandList.BindVertex(frameInfo.CommandBuffer, targetVertex, 0);

      _renderer.CommandList.DrawIndexedIndirect(
        frameInfo.CommandBuffer,
        _indirectComplexBuffers[container.Key]!.GetBuffer(),
        0,
        (uint)container.Value.Commands.Count,
        (uint)Unsafe.SizeOf<VkDrawIndexedIndirectCommand>()
      );
    }
  }

  public unsafe void Render_Indirect_BC(FrameInfo frameInfo) {
    BindPipeline(frameInfo.CommandBuffer, Skinned3D);

    Descriptor.BindDescriptorSet(_device, _textureManager.AllTexturesDescriptor, frameInfo, _pipelines[Skinned3D].PipelineLayout, 0, 1);
    Descriptor.BindDescriptorSet(_device, frameInfo.GlobalDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 1, 1);
    Descriptor.BindDescriptorSet(_device, frameInfo.ObjectDataDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 2, 1);
    Descriptor.BindDescriptorSet(_device, frameInfo.PointLightsDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 3, 1);
    Descriptor.BindDescriptorSet(_device, frameInfo.JointsBufferDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 4, 1);

    // _renderer.CommandList.BindVertex(frameInfo.CommandBuffer, _globalVertexBuffer, 0);
    // _renderer.CommandList.BindIndex(frameInfo.CommandBuffer, _globalIndexBuffer!, 0);
    // _renderer.CommandList.DrawIndexedIndirect(
    //   frameInfo.CommandBuffer,
    //   _indirectBuffer!.GetBuffer(),
    //   0,
    //   (uint)_indirectDrawCommands.Count,
    //   (uint)Unsafe.SizeOf<VkDrawIndexedIndirectCommand>()
    // );

    // Logger.Info("render");
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
    // if TextureIdReference is a value type, using ToString() for dict key is fine,
    // otherwise you can change the dictionary key to Guid directly
    var key = textureId.ToString();
    if (_texIndexCache.TryGetValue(key, out var texId))
      return texId;

    var targetTexture = _textureManager.GetTextureLocal(textureId);
    texId = GetIndexOfMyTexture(targetTexture.TextureName);
    _texIndexCache[key] = texId;
    return texId;
  }

  // NEW
  private unsafe uint RefillIndirectBuffersWithCulling(ConcurrentDictionary<Guid, Mesh> meshes, out IEnumerable<Node> animatedNodes) {
    // clear scratch
    foreach (var s in _simpleVisibleScratch.Values) s.Clear();
    foreach (var s in _complexVisibleScratch.Values) s.Clear();

    var planes = Frustum.BuildFromCamera(CameraState.GetCamera());

    // --- SIMPLE (non-skinned) ---
    List<Node> visSimpleIn;
    Frustum.FilterNodesByFog([.. _simpleBufferNodes], out visSimpleIn); // your culling
    // Frustum.FilterNodesByPlanes(in planes, [.. _simpleBufferNodes], out visSimpleIn);

    foreach (var n in visSimpleIn) {
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
      uint pool = kv.Key;
      var list = kv.Value;
      var buf = _indirectSimpleBuffers[pool];
      var data = _indirectSimples[pool];

      int count = list.Count;
      data.VisibleCount = count;

      if (count == 0) continue;

      fixed (VkDrawIndexedIndirectCommand* p = list.ToArray()) {
        ulong bytes = (ulong)count * (ulong)Unsafe.SizeOf<VkDrawIndexedIndirectCommand>();
        buf.WriteToBuffer((nint)p, bytes, 0);
        buf.Flush(bytes, 0);
      }
    }

    // --- COMPLEX (skinned) ---
    List<Node> visComplexIn;
    Frustum.FilterNodesByFog(new List<Node>(_complexBufferNodes), out visComplexIn);
    animatedNodes = visComplexIn;
    // Frustum.FilterNodesByPlanes(in planes, [.. _complexBufferNodes], out visComplexIn);

    foreach (var n in visComplexIn) {
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
      uint pool = kv.Key;
      var list = kv.Value;
      var buf = _indirectComplexBuffers[pool];
      var data = _indirectComplexes[pool];

      int count = list.Count;
      data.VisibleCount = count;

      if (count == 0) continue;

      fixed (VkDrawIndexedIndirectCommand* p = list.ToArray()) {
        ulong bytes = (ulong)count * (ulong)Unsafe.SizeOf<VkDrawIndexedIndirectCommand>();
        buf.WriteToBuffer((nint)p, bytes, 0);
        buf.Flush(bytes, 0);
      }
    }

    // total visible (for your PerfMonitor)
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

    // (optional) reset/trim texture id cache if topology changed a lot
    // _texIndexCache.Clear();

    // --- GPU buffers that depend on topology/order only ---
    Logger.Info("[RENDER 3D] Recreating Buffers");
    if (notCount > 0) {
      CreateSimpleVertexBuffer(NotSkinnedNodesCache, meshes);
      CreateSimpleIndexBuffer(NotSkinnedNodesCache, meshes);
    }
    if (skinCount > 0) {
      CreateComplexVertexBuffer(SkinnedNodesCache, meshes);
      CreateComplexIndexBuffer(SkinnedNodesCache, meshes);
    }

    EnsureIndirectBuffers(_indirectSimples, ref _indirectSimpleBuffers);
    EnsureIndirectBuffers(_indirectComplexes, ref _indirectComplexBuffers);

    // remember the inputs we built for
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
      // Joints offset is zero for non-skinned; can set once:
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

      // You can also set JointsBufferOffset once here, since it's topology-stable:
      // (we still refresh it in Update to match your requirement list)
      if (_nodeToJointsOffset.TryGetValue(node, out var joff))
        od.JointsBufferOffset = new Vector4(joff, 0, 0, 0);
    }
  }

  // private void CreateOrUpdateBuffers_(IRender3DElement[] renderables) {
  //   // _cacheMatch = _simpleBufferNodes.SequenceEqual(_notSkinnedNodesCache) && _complexBufferNodes.SequenceEqual(_skinnedNodesCache);
  //   _cacheMatch = _renderablesCache.SequenceEqual(renderables);

  //   if (_cacheMatch && _complexBufferPool != null && _simpleBufferPool != null) return;

  //   // Node[] items = [.. _notSkinnedNodesCache, .. _skinnedNodesCache];

  //   _instanceIndexSimple = 0;
  //   _instanceIndexComplex = 0;

  //   Frustum.FlattenNodes(renderables, out var flatNodes);
  //   _flatNodesCache = [.. flatNodes];



  //   Logger.Info("[RENDER 3D] Recreating Buffers");
  //   if (NotSkinnedNodesCache.Length > 0) {
  //     CreateSimpleVertexBuffer(NotSkinnedNodesCache, meshes);
  //     CreateSimpleIndexBuffer(NotSkinnedNodesCache, meshes);
  //   }
  //   if (SkinnedNodesCache.Length > 0) {
  //     CreateComplexVertexBuffer(SkinnedNodesCache, meshes);
  //     CreateComplexIndexBuffer(SkinnedNodesCache, meshes);
  //   }


  //   // CreateIndirectCommands(items);
  //   CreateIndirectBuffer(ref _indirectSimples, ref _indirectSimpleBuffers);
  //   CreateIndirectBuffer(ref _indirectComplexes, ref _indirectComplexBuffers);

  //   _cacheMatch = true;
  // }

  private int AddIndirectCommand(
  ConcurrentDictionary<Guid, Mesh> meshes,
  uint index,                    // vertex pool id
  Node node,
  VertexBinding vertexBinding,
  ref Dictionary<uint, IndirectData> pair,
  ref uint instanceIndex,
  in uint additionalIndexOffset = 0
) {
    if (!pair.ContainsKey(index)) {
      var id = (uint)pair.Keys.Count;
      pair.Add(id, new IndirectData());
    }

    var data = pair[index];
    var mesh = meshes[node.MeshGuid];
    if (mesh?.IndexCount < 1) throw new ArgumentNullException("mesh does not have indices", nameof(mesh));

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

  // private void CreateIndirectCommands(ReadOnlySpan<Node> nodes) {
  //   _indirectDrawCommands.Clear();
  //   uint indexOffset = 0;
  //   uint i = 0;

  //   foreach (var node in nodes) {
  //     var mesh = node.Mesh;
  //     if (mesh?.IndexBuffer == null)
  //       continue;

  //     var cmd = new VkDrawIndexedIndirectCommand {
  //       indexCount = (uint)mesh.Indices.Length,
  //       instanceCount = 1,
  //       firstIndex = indexOffset,
  //       vertexOffset = 0,
  //       firstInstance = (uint)i
  //     };

  //     _indirectDrawCommands.Add(cmd);
  //     indexOffset += (uint)mesh.Indices.Length;
  //     i++;
  //   }
  // }

  // private void CreateIndirectCommands(ReadOnlySpan<IRender3DElement> drawables) {
  //   _indirectDrawCommands.Clear();
  //   uint indexOffset = 0;

  //   for (int i = 0; i < drawables.Length; i++) {
  //     foreach (var node in drawables[i].MeshedNodes) {
  //       var mesh = node.Mesh;
  //       if (mesh?.IndexBuffer == null)
  //         continue;

  //       var cmd = new VkDrawIndexedIndirectCommand {
  //         indexCount = (uint)mesh.Indices.Length,
  //         instanceCount = 1,
  //         firstIndex = indexOffset,
  //         vertexOffset = 0,
  //         firstInstance = (uint)i
  //       };

  //       _indirectDrawCommands.Add(cmd);
  //       indexOffset += (uint)mesh.Indices.Length;
  //     }
  //   }
  // }

  private unsafe void EnsureIndirectBuffers(
  Dictionary<uint, IndirectData> pair,
  ref DwarfBuffer[] buffArray
) {
    // Make sure the array can index directly by 'pool key'
    int maxKey = pair.Count == 0 ? -1 : (int)pair.Keys.Max();
    if (maxKey < 0) { buffArray = Array.Empty<DwarfBuffer>(); return; }

    if (buffArray == null || buffArray.Length <= maxKey) {
      // grow but DO NOT touch existing buffers
      var newArr = new DwarfBuffer[maxKey + 1];
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

      existing?.Dispose(); // only if size insufficient

      var inBuff = new DwarfBuffer(
        _allocator,
        _device,
        neededBytes,
        BufferUsage.IndirectBuffer,                                    // no TransferDst, we write directly
        MemoryProperty.HostVisible | MemoryProperty.HostCoherent,       // CPU visible, persistent
        stagingBuffer: false,
        cpuAccessible: true
      );

      inBuff.Map(neededBytes); // persistently mapped

      buffArray[pool] = inBuff;

      // (optional init): pack all commands once; next frames we overwrite head with visible subset
      if (data.Commands.Count > 0) {
        fixed (VkDrawIndexedIndirectCommand* p = data.Commands.ToArray()) {
          inBuff.WriteToBuffer((nint)p, neededBytes, 0);
          inBuff.Flush(neededBytes, 0);
        }
      }
    }
  }

  private unsafe void CreateIndirectBufferXD(ref Dictionary<uint, IndirectData> pair, ref DwarfBuffer[] buffArray) {
    foreach (var buff in buffArray) {
      buff?.Dispose();
    }
    Array.Clear(buffArray);
    buffArray = new DwarfBuffer[pair.Keys.Count];
    int i = 0;

    foreach (var commands in pair) {
      var size = commands.Value.Commands.Count * Unsafe.SizeOf<VkDrawIndexedIndirectCommand>();

      var stagingBuffer = new DwarfBuffer(
        _allocator,
        _device,
        (ulong)size,
        BufferUsage.TransferSrc,
        MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
        stagingBuffer: true,
        cpuAccessible: true
      );

      stagingBuffer.Map((ulong)size);
      fixed (VkDrawIndexedIndirectCommand* pIndirectCommands = commands.Value.Commands.ToArray()) {
        stagingBuffer.WriteToBuffer((nint)pIndirectCommands, (ulong)size);
      }

      var inBuff = new DwarfBuffer(
        _allocator,
        _device,
        (ulong)size,
        BufferUsage.TransferDst | BufferUsage.IndirectBuffer,
        MemoryProperty.DeviceLocal
      );

      _device.CopyBuffer(stagingBuffer.GetBuffer(), inBuff.GetBuffer(), (ulong)size);
      stagingBuffer.Dispose();

      buffArray[i] = inBuff;
      i++;
    }
  }

  private unsafe void CreateSimpleIndexBuffer(ReadOnlySpan<Node> nodes, ConcurrentDictionary<Guid, Mesh> meshes) {
    _globalSimpleIndexBuffer?.Dispose();

    var adjustedIndices = new List<uint>();
    uint vertexOffset = 0;
    uint indexOffset = 0;

    foreach (var node in nodes) {
      var mesh = meshes[node.MeshGuid];
      if (mesh?.IndexCount < 1) continue;

      foreach (var idx in mesh!.Indices) {
        adjustedIndices.Add(idx + vertexOffset);
      }

      vertexOffset += (uint)mesh.Vertices.Length;
      indexOffset += (uint)mesh.Indices.Length;
    }


    var indexByteSize = (ulong)adjustedIndices.Count * sizeof(uint);

    var stagingBuffer = new DwarfBuffer(
      _allocator,
      _device,
      indexByteSize,
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
      stagingBuffer: true,
      cpuAccessible: true
    );

    stagingBuffer.Map(indexByteSize);
    fixed (uint* pSrc = adjustedIndices.ToArray()) {
      stagingBuffer.WriteToBuffer((nint)pSrc, indexByteSize);
    }

    _globalSimpleIndexBuffer = new DwarfBuffer(
      _allocator,
      _device,
      (ulong)Unsafe.SizeOf<uint>(),
      (uint)adjustedIndices.Count,
      BufferUsage.IndexBuffer | BufferUsage.TransferDst,
      MemoryProperty.DeviceLocal
    );

    _device.CopyBuffer(
      stagingBuffer.GetBuffer(),
      _globalSimpleIndexBuffer.GetBuffer(),
      indexByteSize
    );
    stagingBuffer.Dispose();
  }

  private unsafe void CreateComplexIndexBuffer(ReadOnlySpan<Node> nodes, ConcurrentDictionary<Guid, Mesh> meshes) {
    _globalComplexIndexBuffer?.Dispose();

    var adjustedIndices = new List<uint>();
    uint vertexOffset = 0;
    uint indexOffset = 0;

    foreach (var node in nodes) {
      var mesh = meshes[node.MeshGuid];
      if (mesh?.IndexCount < 1) continue;

      foreach (var idx in mesh!.Indices) {
        adjustedIndices.Add(idx + vertexOffset);
      }

      vertexOffset += (uint)mesh.Vertices.Length;
      indexOffset += (uint)mesh.Indices.Length;
    }


    var indexByteSize = (ulong)adjustedIndices.Count * sizeof(uint);

    var stagingBuffer = new DwarfBuffer(
      _allocator,
      _device,
      indexByteSize,
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
      stagingBuffer: true,
      cpuAccessible: true
    );

    stagingBuffer.Map(indexByteSize);
    fixed (uint* pSrc = adjustedIndices.ToArray()) {
      stagingBuffer.WriteToBuffer((nint)pSrc, indexByteSize);
    }

    _globalComplexIndexBuffer = new DwarfBuffer(
      _allocator,
      _device,
      (ulong)Unsafe.SizeOf<uint>(),
      (uint)adjustedIndices.Count,
      BufferUsage.IndexBuffer | BufferUsage.TransferDst,
      MemoryProperty.DeviceLocal
    );

    _device.CopyBuffer(
      stagingBuffer.GetBuffer(),
      _globalComplexIndexBuffer.GetBuffer(),
      indexByteSize
    );
    stagingBuffer.Dispose();
  }
  private unsafe void CreateSimpleVertexBuffer(ReadOnlySpan<Node> nodes, ConcurrentDictionary<Guid, Mesh> meshes) {
    _vertexSimpleBindings.Clear();
    _simpleBufferPool.Dispose();
    _simpleBufferPool = new BufferPool(_device, _allocator);
    _indirectSimples.Clear();
    _indirectSimples = [];
    uint currentPool = 0;
    uint indexOffset = 0;
    uint vertexOffset = 0;

    var previousSize = 0ul;

    _simpleBufferNodes = nodes.ToArray();
    foreach (var node in nodes) {
      var verts = meshes[node.MeshGuid].Vertices;
      var byteSize = (ulong)verts.Length * (ulong)Unsafe.SizeOf<Vertex>();

      var indices = meshes[node.MeshGuid].Indices;
      var byteSizeIndices = (ulong)indices.Length * sizeof(uint);


      var staging = new DwarfBuffer(
                _allocator, _device, byteSize,
                BufferUsage.TransferSrc,
                MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
                stagingBuffer: true, cpuAccessible: true
              );
      staging.Map(byteSize);
      fixed (Vertex* p = verts) {
        staging.WriteToBuffer((nint)p, byteSize);

        if (!_simpleBufferPool.AddToBuffer(currentPool, (nint)p, byteSize, previousSize, out var byteOffset, out var reason)) {
          var r = reason;
          currentPool = (uint)_simpleBufferPool.AddToPool();
          _simpleBufferPool.AddToBuffer(currentPool, (nint)p, byteSize, previousSize, out byteOffset, out reason);
          // Logger.Info($"[{r}] Creating {currentPool} for {node.Name} offseting by [{byteOffset}] with [{byteSize}] bytes");
        } else {
          // Logger.Info($"[{reason}] Adding {node.Name} to buffer {currentPool} offseting by [{byteOffset}] with [{byteSize}] bytes");
        }
        previousSize += byteSize;

        _vertexSimpleBindings.Add(new VertexBinding {
          BufferIndex = currentPool,
          FirstVertexOffset = (uint)(byteOffset / (ulong)Unsafe.SizeOf<Vertex>()),
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
        vertexOffset += (uint)verts.Length;

        staging.Dispose();
      }
    }
  }

  private unsafe void CreateComplexVertexBuffer(ReadOnlySpan<Node> nodes, ConcurrentDictionary<Guid, Mesh> meshes) {
    _vertexComplexBindings.Clear();
    _complexBufferPool.Dispose();
    _complexBufferPool = new BufferPool(_device, _allocator);
    _indirectComplexes.Clear();
    _indirectComplexes = [];
    uint currentPool = 0;
    uint indexOffset = 0;
    uint vertexOffset = 0;

    var previousSize = 0ul;

    _complexBufferNodes = nodes.ToArray();
    foreach (var node in nodes) {
      var verts = meshes[node.MeshGuid].Vertices;
      var byteSize = (ulong)verts.Length * (ulong)Unsafe.SizeOf<Vertex>();

      var indices = meshes[node.MeshGuid].Indices;
      var byteSizeIndices = (ulong)indices.Length * sizeof(uint);


      var staging = new DwarfBuffer(
                _allocator, _device, byteSize,
                BufferUsage.TransferSrc,
                MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
                stagingBuffer: true, cpuAccessible: true
              );
      staging.Map(byteSize);
      fixed (Vertex* p = verts) {
        staging.WriteToBuffer((nint)p, byteSize);

        if (!_complexBufferPool.AddToBuffer(currentPool, (nint)p, byteSize, previousSize, out var byteOffset, out var reason)) {
          var r = reason;
          currentPool = (uint)_complexBufferPool.AddToPool();
          _complexBufferPool.AddToBuffer(currentPool, (nint)p, byteSize, previousSize, out byteOffset, out reason);
          // Logger.Info($"[{r}] Creating {currentPool} for {node.Name} offseting by [{byteOffset}] with [{byteSize}] bytes");
        } else {
          // Logger.Info($"[{reason}] Adding {node.Name} to buffer {currentPool} offseting by [{byteOffset}] with [{byteSize}] bytes");
        }
        previousSize += byteSize;

        _vertexComplexBindings.Add(new VertexBinding {
          BufferIndex = currentPool,
          FirstVertexOffset = (uint)(byteOffset / (ulong)Unsafe.SizeOf<Vertex>()),
          FirstIndexOffset = indexOffset
        });

        int cmdIdx = AddIndirectCommand(
          meshes,
          currentPool,
          node,
          _vertexComplexBindings.Last(),
          ref _indirectComplexes,
          ref _instanceIndexComplex,
          in _instanceIndexSimple  // keep your base offset for skinned
        );
        _complexCmdMap[node] = new CmdRef(currentPool, cmdIdx);

        indexOffset += (uint)indices.Length;
        vertexOffset += (uint)verts.Length;

        staging.Dispose();
      }
    }
  }

  public override unsafe void Dispose() {
    _device.WaitQueue();
    _globalComplexIndexBuffer?.Dispose();
    _globalSimpleIndexBuffer?.Dispose();
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
    _skinnedGroups = [];
    _notSkinnedGroups = [];
    _indirectSimples.Clear();
    _indirectComplexes.Clear();
    base.Dispose();
  }
}