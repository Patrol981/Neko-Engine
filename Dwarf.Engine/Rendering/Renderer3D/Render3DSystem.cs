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

public partial class Render3DSystem : SystemBase, IRenderSystem {
  public const string Simple3D = "simple3D";
  public const string Skinned3D = "skinned3D";

  public const string HatchTextureName = "./Resources/T_crossHatching13_D.png";
  public static float HatchScale = 1;

  public int LastElemRenderedCount => _notSkinnedNodesCache.Length + _skinnedNodesCache.Length;
  public int LastKnownElemCount { get; private set; } = 0;
  public ulong LastKnownElemSize { get; private set; } = 0;
  public ulong LastKnownSkinnedElemCount { get; private set; }
  public ulong LastKnownSkinnedElemJointsCount { get; private set; }

  private DwarfBuffer? _globalIndexBuffer;
  private DwarfBuffer? _globalVertexBuffer;
  private DwarfBuffer[] _indirectBuffers = [];

  private readonly IDescriptorSetLayout _jointDescriptorLayout = null!;

  internal class IndirectData {
    internal List<VkDrawIndexedIndirectCommand> Commands = [];
    internal uint CurrentIndexOffset = 0;
    internal uint InstanceIndex = 0;
  }
  private List<VkDrawIndexedIndirectCommand> _indirectDrawCommands = [];
  private Dictionary<uint, IndirectData> _indirectPairs = [];
  private uint _instanceIndex = 0;

  private Node[] _notSkinnedNodesCache = [];
  private Node[] _skinnedNodesCache = [];

  private List<(string, int)> _skinnedGroups = [];
  private List<(string, int)> _notSkinnedGroups = [];

  private ITexture _hatchTexture = null!;
  private IDescriptorSetLayout _previousTexturesLayout = null!;

  private BufferPool _bufferPool = null!;
  private List<VertexBinding> _vertexBindings = [];
  private List<IndexBinding> _indexBindings = [];
  private bool _cacheMatch = false;

  public Node[] CachedNodes => [.. _notSkinnedNodesCache, .. _skinnedNodesCache];

  public Render3DSystem(
    nint allocator,
    IDevice device,
    IRenderer renderer,
    TextureManager textureManager,
    Dictionary<string, IDescriptorSetLayout> externalLayouts,
    IPipelineConfigInfo configInfo = null!
  ) : base(allocator, device, renderer, textureManager, configInfo) {
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
      PipelineProvider = new PipelineModelProvider(),
      DescriptorSetLayouts = [.. basicLayouts],
      PipelineName = Simple3D
    });

    AddPipelineData(new() {
      RenderPass = renderer.GetSwapchainRenderPass(),
      VertexName = "model_skinned_vertex",
      FragmentName = "model_skinned_fragment",
      PipelineProvider = new PipelineModelProvider(),
      DescriptorSetLayouts = [.. complexLayouts],
      PipelineName = Skinned3D
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

  public void Setup(ReadOnlySpan<Entity> entities, ref TextureManager textures) {
    if (entities.Length < 1) {
      Logger.Warn("Entities that are capable of using 3D renderer are less than 1, thus 3D Render System won't be recreated");
      return;
    }

    LastKnownElemCount = CalculateNodesLength(entities);
    LastKnownElemSize = 0;
    var (len, joints) = CalculateNodesLengthWithSkin(entities);
    LastKnownSkinnedElemCount = (ulong)len;
    LastKnownSkinnedElemJointsCount = (ulong)joints;

    Logger.Info($"Recreating Renderer 3D [{LastKnownSkinnedElemCount}]");

    for (int i = 0; i < entities.Length; i++) {
      var targetModel = entities[i].GetDrawable<IRender3DElement>() as IRender3DElement;
      LastKnownElemSize += targetModel!.CalculateBufferSize();
    }

    // CreateVertexBuffer(entities);
    // CreateIndexBuffer(entities);

    // CreateIndirectCommands(entities);
    //. CreateIndirectBuffer();
  }

  public void Update(
    Span<IRender3DElement> entities,
    out ObjectData[] objectData,
    out ObjectData[] skinnedObjects,
    out List<Matrix4x4> flatJoints
  ) {
    if (entities.Length < 1) {
      objectData = [];
      skinnedObjects = [];
      flatJoints = [];
      return;
    }

    PerfMonitor.ComunnalStopwatch.Restart();
    Frustum.GetFrustrum(out var planes);
    // entities = Frustum.FilterObjectsByPlanes(in planes, entities).ToArray();
    Frustum.FlattenNodes(entities, out var flattenNodes);
    // Frustum.FilterNodesByPlanes(planes, flattenNodes, out var frustumNodes);
    Frustum.FilterNodesByFog(flattenNodes, out var frustumNodes);

    List<KeyValuePair<Node, ObjectData>> nodeObjectsSkinned = [];
    List<KeyValuePair<Node, ObjectData>> nodeObjectsNotSkinned = [];

    int offset = 0;
    flatJoints = [];

    foreach (var node in frustumNodes) {
      var transform = node.ParentRenderer.GetOwner().GetComponent<Transform>();
      var material = node.ParentRenderer.GetOwner().GetComponent<MaterialComponent>();
      var targetTexture = _textureManager.GetTextureLocal(node.Mesh!.TextureIdReference);
      float texId = (float)GetIndexOfMyTexture(targetTexture.TextureName)!;

      if (node.HasSkin) {
        nodeObjectsSkinned.Add(
          new(
            node,
            new ObjectData {
              ModelMatrix = transform.Matrix4,
              NormalMatrix = transform.NormalMatrix,
              NodeMatrix = node.Mesh!.Matrix,
              JointsBufferOffset = new Vector4(offset, 0, 0, 0),
              ColorAndFilterFlag = new Vector4(material.Color, node.FilterMeInShader == true ? 1 : 0),
              AmbientAndTexId0 = new Vector4(material.Ambient, texId),
              DiffuseAndTexId1 = new Vector4(material.Diffuse, texId),
              SpecularAndShininess = new Vector4(material.Specular, material.Shininess)
            }
          )
        );
        flatJoints.AddRange(node.Skin!.OutputNodeMatrices);
        offset += node.Skin!.OutputNodeMatrices.Length;
      } else {
        nodeObjectsNotSkinned.Add(
          new(
            node,
            new ObjectData {
              ModelMatrix = transform.Matrix4,
              NormalMatrix = transform.NormalMatrix,
              NodeMatrix = node.Mesh!.Matrix,
              JointsBufferOffset = Vector4.Zero,
              ColorAndFilterFlag = new Vector4(material.Color, node.FilterMeInShader == true ? 1 : 0),
              AmbientAndTexId0 = new Vector4(material.Ambient, texId),
              DiffuseAndTexId1 = new Vector4(material.Diffuse, texId),
              SpecularAndShininess = new Vector4(material.Specular, material.Shininess)
            }
          )
        );
      }
    }

    nodeObjectsSkinned.Sort((x, y) => x.Key.CompareTo(y.Key));
    nodeObjectsNotSkinned.Sort((x, y) => x.Key.CompareTo(y.Key));

    _skinnedGroups = [.. nodeObjectsSkinned
      .GroupBy(x => x.Key.Name)
      .Select(group => (Key: group.Key, Count: group.Count()))];

    _notSkinnedGroups = [.. nodeObjectsNotSkinned
      .GroupBy(x => x.Key.Name)
      .Select(group => (Key: group.Key, Count: group.Count()))];

    _skinnedNodesCache = [.. nodeObjectsSkinned.Select(x => x.Key)];
    _notSkinnedNodesCache = [.. nodeObjectsNotSkinned.Select(x => x.Key)];

    objectData = [.. nodeObjectsNotSkinned.Select(x => x.Value), .. nodeObjectsSkinned.Select(x => x.Value)];
    skinnedObjects = [.. nodeObjectsSkinned.Select(x => x.Value)];

    PerfMonitor.Render3DComputeTime = PerfMonitor.ComunnalStopwatch.ElapsedMilliseconds;
  }

  public void Render(FrameInfo frameInfo, bool indirect = true) {
    PerfMonitor.Clear3DRendererInfo();
    PerfMonitor.NumberOfObjectsRenderedIn3DRenderer = (uint)LastElemRenderedCount;
    CreateOrUpdateBuffers();

    if (indirect) {
      RenderIndirect(frameInfo);
    } else {
      if (_notSkinnedNodesCache.Length > 0) {
        RenderSimple(frameInfo, _notSkinnedNodesCache);
      }
      if (_skinnedNodesCache.Length > 0) {
        RenderComplex(frameInfo, _skinnedNodesCache, _notSkinnedNodesCache.Length);
      }
    }


  }

  public unsafe void RenderSimple(FrameInfo frameInfo, Span<Node> nodes) {
    BindPipeline(frameInfo.CommandBuffer, Simple3D);

    Descriptor.BindDescriptorSet(_textureManager.AllTexturesDescriptor, frameInfo, _pipelines[Simple3D].PipelineLayout, 0, 1);
    Descriptor.BindDescriptorSet(frameInfo.GlobalDescriptorSet, frameInfo, _pipelines[Simple3D].PipelineLayout, 1, 1);
    Descriptor.BindDescriptorSet(frameInfo.ObjectDataDescriptorSet, frameInfo, _pipelines[Simple3D].PipelineLayout, 2, 1);
    Descriptor.BindDescriptorSet(frameInfo.PointLightsDescriptorSet, frameInfo, _pipelines[Simple3D].PipelineLayout, 3, 1);

    uint lastBuffer = uint.MaxValue;
    uint indexOffset = 0;

    _renderer.CommandList.BindIndex(frameInfo.CommandBuffer, _globalIndexBuffer!, 0);

    for (int i = 0; i < nodes.Length; i++) {
      if (nodes[i].ParentRenderer.GetOwner().CanBeDisposed || !nodes[i].ParentRenderer.GetOwner().Active) continue;
      if (!nodes[i].ParentRenderer.FinishedInitialization) continue;

      uint thisCount = (uint)nodes[i].Mesh!.Indices.Length;
      var bindInfo = _vertexBindings[i];

      // If we need to switch buffers, bind the new buffer
      if (bindInfo.BufferIndex != lastBuffer) {
        // Get the buffer from the pool and bind it
        var buffer = _bufferPool.GetVertexBuffer(bindInfo.BufferIndex);
        _renderer.CommandList.BindVertex(frameInfo.CommandBuffer, buffer, 0);
        lastBuffer = bindInfo.BufferIndex;
      }

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

  public unsafe void RenderComplex(FrameInfo frameInfo, Span<Node> nodes, int offset) {
    BindPipeline(frameInfo.CommandBuffer, Skinned3D);

    Descriptor.BindDescriptorSet(_textureManager.AllTexturesDescriptor, frameInfo, _pipelines[Skinned3D].PipelineLayout, 0, 1);
    Descriptor.BindDescriptorSet(frameInfo.GlobalDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 1, 1);
    Descriptor.BindDescriptorSet(frameInfo.ObjectDataDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 2, 1);
    Descriptor.BindDescriptorSet(frameInfo.PointLightsDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 3, 1);
    Descriptor.BindDescriptorSet(frameInfo.JointsBufferDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 4, 1);

    uint lastBuffer = uint.MaxValue;
    uint indexOffset = 0;

    _renderer.CommandList.BindIndex(frameInfo.CommandBuffer, _globalIndexBuffer!, 0);

    for (int i = 0; i < nodes.Length; i++) {
      uint thisCount = (uint)nodes[i].Mesh!.Indices.Length;

      if (!nodes[i].ParentRenderer.GetOwner().Active || nodes[i].ParentRenderer.GetOwner().CanBeDisposed || nodes[i].Mesh?.IndexBuffer == null) {
        indexOffset += thisCount;
        continue;
      }

      var bindInfo = _vertexBindings[i + offset];

      if (bindInfo.BufferIndex != lastBuffer) {


        lastBuffer = bindInfo.BufferIndex;
      }

      var vertexBuffer = _bufferPool.GetVertexBuffer(bindInfo.BufferIndex);

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

  public unsafe void RenderIndirect(FrameInfo frameInfo) {
    BindPipeline(frameInfo.CommandBuffer, Skinned3D);

    Descriptor.BindDescriptorSet(_textureManager.AllTexturesDescriptor, frameInfo, _pipelines[Skinned3D].PipelineLayout, 0, 1);
    Descriptor.BindDescriptorSet(frameInfo.GlobalDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 1, 1);
    Descriptor.BindDescriptorSet(frameInfo.ObjectDataDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 2, 1);
    Descriptor.BindDescriptorSet(frameInfo.PointLightsDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 3, 1);
    Descriptor.BindDescriptorSet(frameInfo.JointsBufferDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 4, 1);

    _renderer.CommandList.BindIndex(frameInfo.CommandBuffer, _globalIndexBuffer!, 0);

    foreach (var container in _indirectPairs) {
      var targetVertex = _bufferPool.GetVertexBuffer(container.Key);
      _renderer.CommandList.BindVertex(frameInfo.CommandBuffer, targetVertex, 0);

      _renderer.CommandList.DrawIndexedIndirect(
        frameInfo.CommandBuffer,
        _indirectBuffers[container.Key]!.GetBuffer(),
        0,
        (uint)container.Value.Commands.Count,
        (uint)Unsafe.SizeOf<VkDrawIndexedIndirectCommand>()
      );
    }
  }

  public unsafe void Render_Indirect_BC(FrameInfo frameInfo) {
    BindPipeline(frameInfo.CommandBuffer, Skinned3D);

    Descriptor.BindDescriptorSet(_textureManager.AllTexturesDescriptor, frameInfo, _pipelines[Skinned3D].PipelineLayout, 0, 1);
    Descriptor.BindDescriptorSet(frameInfo.GlobalDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 1, 1);
    Descriptor.BindDescriptorSet(frameInfo.ObjectDataDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 2, 1);
    Descriptor.BindDescriptorSet(frameInfo.PointLightsDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 3, 1);
    Descriptor.BindDescriptorSet(frameInfo.JointsBufferDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 4, 1);

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

  public bool CheckTextures(ReadOnlySpan<Entity> entities) {
    var len = CalculateLengthOfPool(entities);
    // return len == _texturesCount;

    return true;
  }

  public bool CheckSizes(ReadOnlySpan<Entity> entities) {
    var newCount = CalculateNodesLength(entities);

    if (newCount > LastKnownElemCount) {
      return false;
    }

    return true;
  }

  private static int CalculateLengthOfPool(ReadOnlySpan<Entity> entities) {
    int count = 0;
    for (int i = 0; i < entities.Length; i++) {
      var targetItems = entities[i].GetDrawables<IRender3DElement>();
      for (int j = 0; j < targetItems.Length; j++) {
        var t = targetItems[j] as IRender3DElement;
        count += t!.MeshedNodesCount;
      }

    }
    return count;
  }

  private static int CalculateNodesLength(ReadOnlySpan<Entity> entities) {
    int len = 0;
    foreach (var entity in entities) {
      var i3d = entity.GetDrawable<IRender3DElement>() as IRender3DElement;
      len += i3d!.MeshedNodesCount;
    }
    return len;
  }
  private static int? GetIndexOfMyTexture(string texName) {
    return Application.Instance.TextureManager.PerSceneLoadedTextures.Where(x => x.Value.TextureName == texName).FirstOrDefault().Value.TextureManagerIndex;
  }

  private static (int len, int joints) CalculateNodesLengthWithSkin(ReadOnlySpan<Entity> entities) {
    int len = 0;
    int joints = 0;
    foreach (var entity in entities) {
      var i3d = entity.GetDrawable<IRender3DElement>() as IRender3DElement;
      foreach (var mNode in i3d!.MeshedNodes) {
        if (mNode.HasSkin) {
          len += 1;
          joints += mNode.Skin!.OutputNodeMatrices.Length;
        }
      }
    }
    return (len, joints);
  }

  private bool CacheMatch(in IRender3DElement[] drawables) {
    var cachedSimpleBatchIndices = _notSkinnedNodesCache
      .Select(x => x.BatchId)
      .ToArray();
    var cachedSkinnedBatchIndices = _skinnedNodesCache
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

  private void CreateOrUpdateBuffers() {
    if (_cacheMatch && _bufferPool != null) return;

    Node[] items = [.. _notSkinnedNodesCache, .. _skinnedNodesCache];

    _instanceIndex = 0;

    CreateVertexBuffer(items);
    CreateIndexBuffer(items);

    // CreateIndirectCommands(items);
    CreateIndirectBuffer();

    _cacheMatch = true;
  }

  private void AddIndirectCommand(uint index, Node node, VertexBinding vertexBinding) {
    if (!_indirectPairs.ContainsKey(index)) {
      var id = _indirectPairs.Keys.Count;
      _indirectPairs.Add((uint)id, new());
    }

    var data = _indirectPairs[index];

    var mesh = node.Mesh;
    if (mesh?.IndexBuffer == null) throw new ArgumentNullException("mesh does not have index buffer", nameof(mesh));

    var cmd = new VkDrawIndexedIndirectCommand {
      indexCount = (uint)mesh.Indices.Length,
      instanceCount = 1,
      firstIndex = vertexBinding.FirstIndexOffset,
      vertexOffset = (int)vertexBinding.FirstVertexOffset,
      firstInstance = _instanceIndex
    };

    _indirectPairs[index].Commands.Add(cmd);

    _instanceIndex++;
    data.CurrentIndexOffset += vertexBinding.FirstIndexOffset;
  }

  private void CreateIndirectCommands(ReadOnlySpan<Node> nodes) {
    _indirectDrawCommands.Clear();
    uint indexOffset = 0;
    uint i = 0;

    foreach (var node in nodes) {
      var mesh = node.Mesh;
      if (mesh?.IndexBuffer == null)
        continue;

      var cmd = new VkDrawIndexedIndirectCommand {
        indexCount = (uint)mesh.Indices.Length,
        instanceCount = 1,
        firstIndex = indexOffset,
        vertexOffset = 0,
        firstInstance = (uint)i
      };

      _indirectDrawCommands.Add(cmd);
      indexOffset += (uint)mesh.Indices.Length;
      i++;
    }
  }

  private void CreateIndirectCommands(ReadOnlySpan<Entity> drawables) {
    _indirectDrawCommands.Clear();
    uint indexOffset = 0;

    for (int i = 0; i < drawables.Length; i++) {
      foreach (var node in drawables[i].GetComponent<MeshRenderer>().MeshedNodes) {
        var mesh = node.Mesh;
        if (mesh?.IndexBuffer == null)
          continue;

        var cmd = new VkDrawIndexedIndirectCommand {
          indexCount = (uint)mesh.Indices.Length,
          instanceCount = 1,
          firstIndex = indexOffset,
          vertexOffset = 0,
          firstInstance = (uint)i
        };

        _indirectDrawCommands.Add(cmd);
        indexOffset += (uint)mesh.Indices.Length;
      }
    }
  }

  private unsafe void CreateIndirectBuffer() {
    foreach (var buff in _indirectBuffers) {
      buff?.Dispose();
    }
    Array.Clear(_indirectBuffers);
    _indirectBuffers = new DwarfBuffer[_indirectPairs.Keys.Count];
    int i = 0;

    foreach (var commands in _indirectPairs) {
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

      _indirectBuffers[i] = inBuff;
      i++;
    }
  }

  private unsafe void CreateIndexBuffer(ReadOnlySpan<Node> nodes) {
    _globalIndexBuffer?.Dispose();
    _indexBindings?.Clear();

    var adjustedIndices = new List<uint>();
    uint vertexOffset = 0;
    uint indexOffset = 0;

    foreach (var node in nodes) {
      var mesh = node.Mesh;
      if (mesh?.IndexBuffer == null) continue;

      foreach (var idx in mesh.Indices) {
        adjustedIndices.Add(idx + vertexOffset);
      }

      _indexBindings?.Add(new() { FirstIndexOffset = indexOffset });

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

    _globalIndexBuffer = new DwarfBuffer(
      _allocator,
      _device,
      (ulong)Unsafe.SizeOf<uint>(),
      (uint)adjustedIndices.Count,
      BufferUsage.IndexBuffer | BufferUsage.TransferDst,
      MemoryProperty.DeviceLocal
    );

    _device.CopyBuffer(
      stagingBuffer.GetBuffer(),
      _globalIndexBuffer.GetBuffer(),
      indexByteSize
    );
    stagingBuffer.Dispose();
  }

  private unsafe void CreateIndexBuffer(ReadOnlySpan<Entity> drawables) {
    _globalIndexBuffer?.Dispose();
    _indexBindings?.Clear();

    var adjustedIndices = new List<uint>();
    uint vertexOffset = 0;
    uint indexOffset = 0;

    for (int i = 0; i < drawables.Length; i++) {
      foreach (var node in drawables[i].GetComponent<MeshRenderer>().MeshedNodes) {
        var mesh = node.Mesh;
        if (mesh?.IndexBuffer == null) continue;

        foreach (var idx in mesh.Indices) {
          adjustedIndices.Add(idx);
        }

        _indexBindings?.Add(new() { FirstIndexOffset = indexOffset });

        vertexOffset += (uint)mesh.Vertices.Length;
        indexOffset += (uint)mesh.Indices.Length;
      }
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

    _globalIndexBuffer = new DwarfBuffer(
      _allocator,
      _device,
      (ulong)Unsafe.SizeOf<uint>(),
      (uint)adjustedIndices.Count,
      BufferUsage.IndexBuffer | BufferUsage.TransferDst,
      MemoryProperty.DeviceLocal
    );

    _device.CopyBuffer(
      stagingBuffer.GetBuffer(),
      _globalIndexBuffer.GetBuffer(),
      indexByteSize
    );
    stagingBuffer.Dispose();
  }

  private unsafe void CreateVertexBuffer(ReadOnlySpan<Node> nodes) {
    _vertexBindings.Clear();
    _bufferPool.Dispose();
    _bufferPool = new BufferPool(_device, _allocator);
    uint currentPool = 0;
    uint indexOffset = 0;
    uint vertexOffset = 0;

    var previousSize = 0ul;

    foreach (var node in nodes) {
      var verts = node.Mesh!.Vertices;
      var byteSize = (ulong)verts.Length * (ulong)Unsafe.SizeOf<Vertex>();

      var indices = node.Mesh!.Indices;
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

        var indexByteOffset = 0ul;
        var testVertexOffset = 0ul;

        if (!_bufferPool.AddToBuffer(currentPool, (nint)p, byteSize, previousSize, out var byteOffset, out var reason)) {
          var r = reason;
          currentPool = (uint)_bufferPool.AddToPool();
          _bufferPool.AddToBuffer(currentPool, (nint)p, byteSize, previousSize, out byteOffset, out reason);
          Logger.Info($"[{r}] Creating {currentPool} for {node.Name} offseting by [{byteOffset}] with [{byteSize}] bytes");
        } else {
          Logger.Info($"[{reason}] Adding {node.Name} to buffer {currentPool} offseting by [{byteOffset}] with [{byteSize}] bytes");
        }
        previousSize += byteSize;

        _vertexBindings.Add(new VertexBinding {
          BufferIndex = currentPool,
          // FirstVertexOffset = (uint)(byteSize / (ulong)Unsafe.SizeOf<Vertex>()),
          // FirstIndexOffset = (uint)(indexByteOffset / (ulong)Unsafe.SizeOf<uint>()),
          FirstVertexOffset = (uint)(byteOffset / (ulong)Unsafe.SizeOf<Vertex>()),
          FirstIndexOffset = indexOffset
        });

        AddIndirectCommand(currentPool, node, _vertexBindings.Last());

        Logger.Warn($"Setting VTX offset to {_vertexBindings.Last().FirstVertexOffset} from {byteOffset}");
        Logger.Warn($"[D] Setting VTX offset to {vertexOffset} {byteSize / (ulong)Unsafe.SizeOf<Vertex>()}");


        indexOffset += (uint)indices.Length;
        vertexOffset += (uint)verts.Length;

        staging.Dispose();
      }
    }
  }

  private unsafe void CreateVertexBuffer(ReadOnlySpan<Entity> drawables) {
    _vertexBindings.Clear();
    _bufferPool.Dispose();
    _bufferPool = new BufferPool(_device, _allocator);
    uint currentPool = 0;

    foreach (var entity in drawables) {
      foreach (var node in entity.GetComponent<MeshRenderer>().MeshedNodes) {
        var verts = node.Mesh!.Vertices;
        var byteSize = (ulong)verts.Length * (ulong)Unsafe.SizeOf<Vertex>();

        var indices = node.Mesh!.Indices;
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

          // if (!_bufferPool.AddToBuffer(currentPool, (nint)p, byteSize, out var byteOffset, out var reason)) {
          //   currentPool = (uint)_bufferPool.AddToPool();
          //   _bufferPool.AddToBuffer(currentPool, (nint)p, byteSize, out byteOffset, out reason);
          //   // _bufferPool.AddToIndex(currentPool, node.Mesh.Indices);
          // } else {
          //   // _bufferPool.AddToIndex(currentPool, node.Mesh.Indices);
          // }

          // _vertexBindings.Add(new VertexBinding {
          //   BufferIndex = currentPool,
          //   FirstVertexOffset = (uint)(byteOffset / (ulong)Unsafe.SizeOf<Vertex>()),
          //   FirstIndexOffset = (uint)byteSizeIndices / sizeof(uint)
          // });

          staging.Dispose();
        }
      }
    }
  }

  public override unsafe void Dispose() {
    _device.WaitQueue();
    _globalVertexBuffer?.Dispose();
    _globalIndexBuffer?.Dispose();
    foreach (var buff in _indirectBuffers) {
      buff.Dispose();
    }
    _bufferPool?.Dispose(); _jointDescriptorLayout?.Dispose();
    base.Dispose();
  }
}