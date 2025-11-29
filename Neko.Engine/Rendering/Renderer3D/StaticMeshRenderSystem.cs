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

public class StaticRenderSystem : SystemBase {
  public const string PipelineName = "Static";

  public Node[] Cache { get; private set; } = [];

  public ulong LastKnownElemSize { get; private set; } = 0;
  public int LastKnownElemCount { get; private set; } = 0;

  private NekoBuffer[] _indirectBuffers = [];
  private Dictionary<uint, IndirectData> _indirectData = [];
  private uint _instanceIndex;

  private BufferPool? _bufferPool;
  private readonly List<VertexBinding> _vertexBindings = [];

  private bool _cacheMatch = false;
  private readonly Dictionary<Guid, float> _texIndexCache = [];

  private ObjectData[] _objectDataScratch = [];

  private readonly Dictionary<Node, int> _nodeToObjIndex = [];

  private IRender3DElement[] _renderablesCache = [];

  private readonly Dictionary<uint, List<VkDrawIndexedIndirectCommand>> _visibleScratch = [];
  private readonly Dictionary<Node, CmdRef> _cmdMap = [];

  public StaticRenderSystem(
   Application app,
   nint allocator,
   VulkanDevice device,
   IRenderer renderer,
   TextureManager textureManager,
   Dictionary<string, IDescriptorSetLayout> externalLayouts,
   IPipelineConfigInfo configInfo = null!
  ) : base(app, allocator, device, renderer, textureManager, configInfo) {
    IDescriptorSetLayout[] layouts = [
      _textureManager.AllTexturesSetLayout,
      externalLayouts["Global"],
      externalLayouts["ObjectData"],
      externalLayouts["PointLight"],
    ];

    AddPipelineData(new() {
      RenderPass = renderer.GetSwapchainRenderPass(),
      VertexName = "model_vertex",
      FragmentName = "model_fragment",
      GeometryName = "model_geometry",
      PipelineProvider = new PipelineModelProvider(),
      DescriptorSetLayouts = [.. layouts],
      PipelineName = PipelineName
    });

    _descriptorPool = new VulkanDescriptorPool.Builder(_device)
      .SetMaxSets(CommonConstants.MAX_SETS)
      .AddPoolSize(DescriptorType.UniformBuffer, CommonConstants.MAX_SETS)
      .AddPoolSize(DescriptorType.SampledImage, CommonConstants.MAX_SETS)
      .AddPoolSize(DescriptorType.Sampler, CommonConstants.MAX_SETS)
      .AddPoolSize(DescriptorType.InputAttachment, CommonConstants.MAX_SETS)
      .AddPoolSize(DescriptorType.StorageBuffer, CommonConstants.MAX_SETS)
      .SetPoolFlags(DescriptorPoolCreateFlags.UpdateAfterBind)
      .Build();

    _bufferPool = new(_device, _allocator);
  }

  public void Setup(ReadOnlySpan<IRender3DElement> renderables) {
    if (renderables.Length < 1) {
      Logger.Warn("Entities that are capable of using static rendering are less than 1, thus [Static Render System] won't be recreated");
      return;
    }

    LastKnownElemCount = CalculateNodesLength(renderables);
  }

  public void Update(
    FrameInfo frameInfo,
    ConcurrentDictionary<Guid, Mesh> meshes,
    out ulong staticOffset
  ) {
    staticOffset = 0;
    if (_objectDataScratch.Length == 0) return;

    for (int i = 0; i < Cache.Length; i++) {
      var node = Cache[i];
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

    unsafe {
      fixed (ObjectData* pObjectData = _objectDataScratch) {
        _application.StorageCollection.WriteToIndex(
          "StaticObjectStorage",
          frameInfo.FrameIndex,
          (nint)pObjectData,
          (ulong)Unsafe.SizeOf<ObjectData>() * (ulong)_objectDataScratch.Length,
          0
        );
      }
    }

    staticOffset = (ulong)Cache.Length;
  }

  public void Render(
    IRender3DElement[] renderables,
    ConcurrentDictionary<Guid, Mesh> meshes,
    FrameInfo frameInfo,
    out IEnumerable<Node> staticNodes
  ) {
    CreateOrUpdateBuffers(renderables, meshes);
    uint visible = RefillIndirectBuffersWithCulling(meshes, out staticNodes);

    if (visible < 1) return;

    RenderIndirect(frameInfo);
  }

  public void RenderIndirect(FrameInfo frameInfo) {
    BindPipeline(frameInfo.CommandBuffer, PipelineName);

    Descriptor.BindDescriptorSet(_device, _textureManager.AllTexturesDescriptor, frameInfo, _pipelines[PipelineName].PipelineLayout, 0, 1);
    Descriptor.BindDescriptorSet(_device, frameInfo.GlobalDescriptorSet, frameInfo, _pipelines[PipelineName].PipelineLayout, 1, 1);
    Descriptor.BindDescriptorSet(_device, frameInfo.StaticObjectDataDescriptorSet, frameInfo, _pipelines[PipelineName].PipelineLayout, 2, 1);
    Descriptor.BindDescriptorSet(_device, frameInfo.PointLightsDescriptorSet, frameInfo, _pipelines[PipelineName].PipelineLayout, 3, 1);

    foreach (var container in _indirectData) {
      var targetIndex = _bufferPool?.GetIndexBuffer(container.Key);
      var targetVertex = _bufferPool?.GetVertexBuffer(container.Key);

      if (targetIndex == null || targetVertex == null) continue;

      _renderer.CommandList.BindIndex(frameInfo.CommandBuffer, targetIndex, 0);
      _renderer.CommandList.BindVertex(frameInfo.CommandBuffer, targetVertex, 0);

      _renderer.CommandList.DrawIndexedIndirect(
        frameInfo.CommandBuffer,
        _indirectBuffers[container.Key]!.GetBuffer(),
        0,
        (uint)container.Value.VisibleCount,
        (uint)Unsafe.SizeOf<VkDrawIndexedIndirectCommand>()
      );
    }
  }

  public void RenderDirect(
    FrameInfo frameInfo,
    Span<Node> nodes,
    ConcurrentDictionary<Guid, Mesh> meshes
  ) {
    BindPipeline(frameInfo.CommandBuffer, PipelineName);

    Descriptor.BindDescriptorSet(_device, _textureManager.AllTexturesDescriptor, frameInfo, _pipelines[PipelineName].PipelineLayout, 0, 1);
    Descriptor.BindDescriptorSet(_device, frameInfo.GlobalDescriptorSet, frameInfo, _pipelines[PipelineName].PipelineLayout, 1, 1);
    Descriptor.BindDescriptorSet(_device, frameInfo.StaticObjectDataDescriptorSet, frameInfo, _pipelines[PipelineName].PipelineLayout, 2, 1);
    Descriptor.BindDescriptorSet(_device, frameInfo.PointLightsDescriptorSet, frameInfo, _pipelines[PipelineName].PipelineLayout, 3, 1);

    uint lastBuffer = uint.MaxValue;
    uint indexOffset = 0;

    // _renderer.CommandList.BindIndex(frameInfo.CommandBuffer, _globalSimpleIndexBuffer!, 0);

    for (int i = 0; i < nodes.Length; i++) {
      if (nodes[i].ParentRenderer.Owner.CanBeDisposed || !nodes[i].ParentRenderer.Owner.Active) continue;
      if (!nodes[i].ParentRenderer.FinishedInitialization) continue;

      uint thisCount = (uint)meshes[nodes[i].MeshGuid].Indices.Length;
      var bindInfo = _vertexBindings[i];

      var vertexBuffer = _bufferPool!.GetVertexBuffer(bindInfo.BufferIndex);
      var indexBuffer = _bufferPool!.GetIndexBuffer(bindInfo.BufferIndex);

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

  public bool CheckSizes(ReadOnlySpan<IRender3DElement> renderables) {
    var newCount = CalculateNodesLength(renderables);

    if (newCount > LastKnownElemCount) {
      return false;
    }

    return true;
  }

  private void CreateOrUpdateBuffers(
    IRender3DElement[] renderables,
    ConcurrentDictionary<Guid, Mesh> meshes
  ) {
    _cacheMatch = _renderablesCache.SequenceEqual(renderables);
    if (_cacheMatch && _bufferPool != null) return;

    _instanceIndex = 0;

    Frustum.FlattenNodes(renderables, out var flatNodes);

    var nodeObjects = new List<Node>(flatNodes.Count / 2);

    foreach (var node in flatNodes) {
      if (!node.Enabled || !node.HasMesh) continue;
      if (!node.HasSkin) nodeObjects.Add(node);
    }

    nodeObjects.Sort(static (a, b) => a.CompareTo(b));

    Cache = [.. nodeObjects];

    int totalObjs = Cache.Length;

    _objectDataScratch = (totalObjs == 0) ? [] : new ObjectData[totalObjs];

    _nodeToObjIndex.Clear();
    for (int i = 0; i < totalObjs; i++) {
      _nodeToObjIndex[Cache[i]] = i;
    }

    _texIndexCache.Clear();

    Logger.Info($"Total Objs: {totalObjs}");

    if (totalObjs > 0) {
      CreateVertexIndexBuffer(Cache, meshes);
    }

    EnsureIndirectBuffers(_indirectData, ref _indirectBuffers);

    _renderablesCache = (IRender3DElement[])renderables.Clone();
    _cacheMatch = true;

    for (int i = 0; i < Cache.Length; i++) {
      var node = Cache[i];
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

    EnsureVisibleScratchCapacity();
  }

  private void EnsureVisibleScratchCapacity() {
    foreach (var (pool, data) in _indirectData) {
      if (!_visibleScratch.TryGetValue(pool, out var list))
        _visibleScratch[pool] = new List<VkDrawIndexedIndirectCommand>(data.Commands.Count);
      else if (list.Capacity < data.Commands.Count)
        list.Capacity = data.Commands.Count;
    }
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

  private static int GetIndexOfMyTexture(string texName) {
    var texturePair = Application.Instance.TextureManager.PerSceneLoadedTextures
      .Where(x => x.Value.TextureName == texName)
      .Single();
    return texturePair.Value.TextureManagerIndex;
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

  private void CreateVertexIndexBuffer(
    ReadOnlySpan<Node> nodes,
    ConcurrentDictionary<Guid, Mesh> meshes
  ) {
    _vertexBindings.Clear();
    _bufferPool?.Dispose();
    _bufferPool = new BufferPool(_device, _allocator);
    _indirectData.Clear();

    var indexOffset = 0u;
    var vertexOffset = 0u;

    var currentPool = 0u;
    _bufferPool.CreateNewBakeData(currentPool);

    var adjustedIndices = new List<uint>();

    var accumulatedIndexSize = 0u;
    var accumulatedVertexSize = 0ul;

    Cache = nodes.ToArray();
    foreach (var node in nodes) {
      ReadOnlySpan<Vertex> verts = meshes[node.MeshGuid].Vertices;
      ReadOnlySpan<uint> indices = meshes[node.MeshGuid].Indices;

      var vertexByteSize = (ulong)verts.Length * (ulong)Unsafe.SizeOf<Vertex>();
      var indexByteSize = (uint)indices.Length * sizeof(uint);

      accumulatedVertexSize += vertexByteSize;
      accumulatedIndexSize += indexByteSize;

      var canAddVertex = _bufferPool.CanBakeMoreVertex(currentPool, accumulatedVertexSize);
      var canAddIndex = _bufferPool.CanBakeMoreIndex(currentPool, accumulatedIndexSize);
      var isOverflowingOnNextStep = CheckForOverflow(indexOffset, (uint)indices.Length);

      if (!canAddIndex || !canAddVertex || isOverflowingOnNextStep) {
        currentPool++;
        _bufferPool.CreateNewBakeData(currentPool);
        indexOffset = 0;
        vertexOffset = 0;
        accumulatedIndexSize = 0;
        accumulatedVertexSize = 0;
      }

      var localIndices = new List<uint>();
      foreach (var idx in meshes[node.MeshGuid].Indices) {
        adjustedIndices.Add(idx + vertexOffset);
        localIndices.Add(idx + vertexOffset);
      }

      _bufferPool.AddIndexToBake(currentPool, [.. indices]);
      _bufferPool.AddVertexToBake(currentPool, [.. verts]);

      _vertexBindings.Add(new VertexBinding {
        BufferIndex = currentPool,
        FirstVertexOffset = vertexOffset,
        FirstIndexOffset = indexOffset
      });

      int cmdIdx = AddIndirectCommand(
        meshes,
        currentPool,
        node,
        _vertexBindings.Last(),
        ref _indirectData,
        ref _instanceIndex
      );
      _cmdMap[node] = new CmdRef(currentPool, cmdIdx);

      indexOffset += (uint)indices.Length;
      vertexOffset += (uint)meshes[node.MeshGuid].Vertices.Length;
    }

    _bufferPool.BakeAll();
  }

  private unsafe void CreateVertexIndexBuffer_NonBaked(
    ReadOnlySpan<Node> nodes,
    ConcurrentDictionary<Guid, Mesh> meshes
  ) {
    _vertexBindings.Clear();
    _bufferPool?.Dispose();
    _bufferPool = new BufferPool(_device, _allocator);
    _indirectData.Clear();

    var currentPool = 0u;
    var indexOffset = 0u;
    var vertexOffset = 0u;

    var adjustedIndices = new List<uint>();
    var previousVertexSize = 0ul;
    var previousIndexSize = 0ul;

    Cache = nodes.ToArray();
    foreach (var node in nodes) {
      ReadOnlySpan<Vertex> verts = meshes[node.MeshGuid].Vertices;
      ReadOnlySpan<uint> indices = meshes[node.MeshGuid].Indices;

      var vertexByteSize = (ulong)verts.Length * (ulong)Unsafe.SizeOf<Vertex>();
      var indexByteSize = (ulong)indices.Length * sizeof(uint);

      ulong vertexByteOffset;
      var canAddVertex = _bufferPool.CanAddToVertexBuffer(
        index: currentPool,
        byteSize: vertexByteSize,
        prevSize: previousVertexSize
      );
      var canAddIndex = _bufferPool.CanAddToIndexBuffer(
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
        currentPool = (uint)_bufferPool.AddToPool();
        previousVertexSize = 0;
        previousIndexSize = 0;
        indexOffset = 0;
        vertexOffset = 0;
      }
      fixed (Vertex* pVertex = verts) {
        _bufferPool.AddToBuffer(currentPool, (nint)pVertex, vertexByteSize, previousVertexSize, out vertexByteOffset, out var reason);
      }

      fixed (uint* pIndicesToPool = localIndices.ToArray()) {
        _bufferPool.AddToIndex(
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

      _vertexBindings.Add(new VertexBinding {
        BufferIndex = currentPool,
        FirstVertexOffset = (uint)(vertexByteOffset / (ulong)Unsafe.SizeOf<Vertex>()),
        FirstIndexOffset = indexOffset
      });

      int cmdIdx = AddIndirectCommand(
        meshes,
        currentPool,
        node,
        _vertexBindings.Last(),
        ref _indirectData,
        ref _instanceIndex
      );
      _cmdMap[node] = new CmdRef(currentPool, cmdIdx);

      indexOffset += (uint)indices.Length;
      vertexOffset += (uint)meshes[node.MeshGuid].Vertices.Length;
    }
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

  private static bool CheckForOverflow(uint a, uint b) {
    if (a > uint.MaxValue - b) {
      return true;
    }
    return false;
  }

  private uint RefillIndirectBuffersWithCulling(
    ConcurrentDictionary<Guid, Mesh> meshes,
    out IEnumerable<Node> staticNodes
  ) {
    if (_indirectBuffers?.Length < 1) { staticNodes = []; return 0; }

    foreach (var s in _visibleScratch.Values) s.Clear();

    Frustum.FilterNodesByFog(Cache, out var visible);
    staticNodes = [.. visible];

    foreach (var n in visible) {
      var owner = n.ParentRenderer.Owner;
      if (owner.CanBeDisposed || !owner.Active) continue;
      if (meshes[n.MeshGuid]?.IndexCount < 1) continue;

      if (!_cmdMap.TryGetValue(n, out var r)) continue;
      var src = _indirectData[r.Pool].Commands[r.CmdIndex];

      if (!_visibleScratch.TryGetValue(r.Pool, out var list)) {
        list = new List<VkDrawIndexedIndirectCommand>(32);
        _visibleScratch[r.Pool] = list;
      }
      list.Add(src);
    }

    foreach (var kv in _visibleScratch) {
      var pool = kv.Key;
      var list = kv.Value;
      _indirectData[pool].VisibleCount = list.Count;
      var targetBuffer = _indirectBuffers![pool];
      CopyCmdListToBuffer(list, targetBuffer);
    }

    uint total = 0;
    foreach (var d in _indirectData.Values) total += (uint)d.VisibleCount;
    return total;
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

  private static int CalculateNodesLength(ReadOnlySpan<IRender3DElement> renderables) {
    int len = 0;
    foreach (var renderable in renderables) {
      len += renderable.MeshedNodesCount;
    }
    return len;
  }

  public override void Dispose() {
    _device.WaitQueue();
    foreach (var buff in _indirectBuffers) {
      buff.Dispose();
    }
    _vertexBindings.Clear();
    _bufferPool?.Dispose();
    LastKnownElemSize = 0;
    LastKnownElemCount = 0;
    _instanceIndex = 0;
    Cache = [];
    _cacheMatch = false;
    _indirectData.Clear();
    base.Dispose();
  }
}