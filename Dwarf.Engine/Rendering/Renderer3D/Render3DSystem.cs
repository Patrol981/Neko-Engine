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

  private Node[] _notSkinnedNodesCache = [];
  private Node[] _skinnedNodesCache = [];

  private List<(string, int)> _skinnedGroups = [];
  private List<(string, int)> _notSkinnedGroups = [];

  private ITexture _hatchTexture = null!;
  private IDescriptorSetLayout _previousTexturesLayout = null!;

  private BufferPool _simpleBufferPool = null!;
  private BufferPool _complexBufferPool = null!;
  private List<VertexBinding> _vertexSimpleBindings = [];
  private List<VertexBinding> _vertexComplexBindings = [];
  private bool _cacheMatch = false;

  public Node[] CachedNodes => [.. _notSkinnedNodesCache, .. _skinnedNodesCache];

  private Node[] _simpleBufferNodes = [];
  private Node[] _complexBufferNodes = [];

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

    _descriptorPool = new VulkanDescriptorPool.Builder(_device)
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
    // Frustum.FilterNodesByFog(flattenNodes, out var frustumNodes);

    List<KeyValuePair<Node, ObjectData>> nodeObjectsSkinned = [];
    List<KeyValuePair<Node, ObjectData>> nodeObjectsNotSkinned = [];

    int offset = 0;
    flatJoints = [];

    foreach (var node in flattenNodes) {
      var transform = node.ParentRenderer.GetOwner().GetComponent<Transform>();
      var material = node.ParentRenderer.GetOwner().GetComponent<MaterialComponent>();
      var targetTexture = _textureManager.GetTextureLocal(node.Mesh!.TextureIdReference);
      float texId = GetIndexOfMyTexture(targetTexture.TextureName)!;

      if (!node.Enabled) continue;

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

    foreach (var skinned in nodeObjectsSkinned) {
      skinned.Key.ParentRenderer.GetOwner().TryGetComponent<AnimationController>()?.Update(skinned.Key);
    }

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
      if (_simpleBufferNodes.Length > 0) {
        RenderSimpleIndirect(frameInfo);
      }
      if (_complexBufferNodes.Length > 0) {
        RenderComplexIndirect(frameInfo);
      }
    } else {
      if (_simpleBufferNodes.Length > 0) {
        RenderSimple(frameInfo, _simpleBufferNodes);
      }
      if (_complexBufferNodes.Length > 0) {
        RenderComplex(frameInfo, _complexBufferNodes, _simpleBufferNodes.Length);
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

    _renderer.CommandList.BindIndex(frameInfo.CommandBuffer, _globalSimpleIndexBuffer!, 0);

    for (int i = 0; i < nodes.Length; i++) {
      if (nodes[i].ParentRenderer.GetOwner().CanBeDisposed || !nodes[i].ParentRenderer.GetOwner().Active) continue;
      if (!nodes[i].ParentRenderer.FinishedInitialization) continue;

      uint thisCount = (uint)nodes[i].Mesh!.Indices.Length;
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

  public unsafe void RenderComplex(FrameInfo frameInfo, Span<Node> nodes, int offset) {
    BindPipeline(frameInfo.CommandBuffer, Skinned3D);

    Descriptor.BindDescriptorSet(_textureManager.AllTexturesDescriptor, frameInfo, _pipelines[Skinned3D].PipelineLayout, 0, 1);
    Descriptor.BindDescriptorSet(frameInfo.GlobalDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 1, 1);
    Descriptor.BindDescriptorSet(frameInfo.ObjectDataDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 2, 1);
    Descriptor.BindDescriptorSet(frameInfo.PointLightsDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 3, 1);
    Descriptor.BindDescriptorSet(frameInfo.JointsBufferDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 4, 1);

    uint lastBuffer = uint.MaxValue;
    uint indexOffset = 0;

    _renderer.CommandList.BindIndex(frameInfo.CommandBuffer, _globalComplexIndexBuffer!, 0);

    for (int i = 0; i < nodes.Length; i++) {
      uint thisCount = (uint)nodes[i].Mesh!.Indices.Length;

      if (!nodes[i].ParentRenderer.GetOwner().Active || nodes[i].ParentRenderer.GetOwner().CanBeDisposed || nodes[i].Mesh?.IndexBuffer == null) {
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

    Descriptor.BindDescriptorSet(_textureManager.AllTexturesDescriptor, frameInfo, _pipelines[Simple3D].PipelineLayout, 0, 1);
    Descriptor.BindDescriptorSet(frameInfo.GlobalDescriptorSet, frameInfo, _pipelines[Simple3D].PipelineLayout, 1, 1);
    Descriptor.BindDescriptorSet(frameInfo.ObjectDataDescriptorSet, frameInfo, _pipelines[Simple3D].PipelineLayout, 2, 1);
    Descriptor.BindDescriptorSet(frameInfo.PointLightsDescriptorSet, frameInfo, _pipelines[Simple3D].PipelineLayout, 3, 1);

    _renderer.CommandList.BindIndex(frameInfo.CommandBuffer, _globalSimpleIndexBuffer!, 0);

    foreach (var container in _indirectSimples) {
      var targetVertex = _simpleBufferPool.GetVertexBuffer(container.Key);
      _renderer.CommandList.BindVertex(frameInfo.CommandBuffer, targetVertex, 0);

      _renderer.CommandList.DrawIndexedIndirect(
        frameInfo.CommandBuffer,
        _indirectSimpleBuffers[container.Key]!.GetBuffer(),
        0,
        (uint)container.Value.Commands.Count,
        (uint)Unsafe.SizeOf<VkDrawIndexedIndirectCommand>()
      );
    }
  }

  public unsafe void RenderComplexIndirect(FrameInfo frameInfo) {
    BindPipeline(frameInfo.CommandBuffer, Skinned3D);

    Descriptor.BindDescriptorSet(_textureManager.AllTexturesDescriptor, frameInfo, _pipelines[Skinned3D].PipelineLayout, 0, 1);
    Descriptor.BindDescriptorSet(frameInfo.GlobalDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 1, 1);
    Descriptor.BindDescriptorSet(frameInfo.ObjectDataDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 2, 1);
    Descriptor.BindDescriptorSet(frameInfo.PointLightsDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 3, 1);
    Descriptor.BindDescriptorSet(frameInfo.JointsBufferDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 4, 1);

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

  public unsafe void RenderIndirect(FrameInfo frameInfo) {
    BindPipeline(frameInfo.CommandBuffer, Skinned3D);

    Descriptor.BindDescriptorSet(_textureManager.AllTexturesDescriptor, frameInfo, _pipelines[Skinned3D].PipelineLayout, 0, 1);
    Descriptor.BindDescriptorSet(frameInfo.GlobalDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 1, 1);
    Descriptor.BindDescriptorSet(frameInfo.ObjectDataDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 2, 1);
    Descriptor.BindDescriptorSet(frameInfo.PointLightsDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 3, 1);
    Descriptor.BindDescriptorSet(frameInfo.JointsBufferDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 4, 1);

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
  private static int GetIndexOfMyTexture(string texName) {
    var texturePair = Application.Instance.TextureManager.PerSceneLoadedTextures.Where(x => x.Value.TextureName == texName).Single();
    return texturePair.Value.TextureManagerIndex;
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
    _cacheMatch = _simpleBufferNodes.SequenceEqual(_notSkinnedNodesCache) && _complexBufferNodes.SequenceEqual(_skinnedNodesCache);

    if (_cacheMatch && _complexBufferPool != null && _simpleBufferPool != null) return;

    // Node[] items = [.. _notSkinnedNodesCache, .. _skinnedNodesCache];

    _instanceIndexSimple = 0;
    _instanceIndexComplex = 0;

    Logger.Info("[RENDER 3D] Recreating Buffers");
    if (_notSkinnedNodesCache.Length > 0) {
      CreateSimpleVertexBuffer(_notSkinnedNodesCache);
      CreateSimpleIndexBuffer(_notSkinnedNodesCache);
    }
    if (_skinnedNodesCache.Length > 0) {
      CreateComplexVertexBuffer(_skinnedNodesCache);
      CreateComplexIndexBuffer(_skinnedNodesCache);
    }


    // CreateIndirectCommands(items);
    CreateIndirectBuffer(ref _indirectSimples, ref _indirectSimpleBuffers);
    CreateIndirectBuffer(ref _indirectComplexes, ref _indirectComplexBuffers);

    _cacheMatch = true;
  }

  private void AddIndirectCommand(
    uint index,
    Node node,
    VertexBinding vertexBinding,
    ref Dictionary<uint, IndirectData> pair,
    ref uint instanceIndex,
    in uint additionalIndexOffset = 0
  ) {
    if (!pair.ContainsKey(index)) {
      var id = pair.Keys.Count;
      pair.Add((uint)id, new());
    }

    var data = pair[index];

    var mesh = node.Mesh;
    if (mesh?.IndexBuffer == null) throw new ArgumentNullException("mesh does not have index buffer", nameof(mesh));

    var cmd = new VkDrawIndexedIndirectCommand {
      indexCount = (uint)mesh.Indices.Length,
      instanceCount = 1,
      firstIndex = vertexBinding.FirstIndexOffset,
      vertexOffset = (int)vertexBinding.FirstVertexOffset,
      firstInstance = instanceIndex + additionalIndexOffset
    };

    pair[index].Commands.Add(cmd);

    instanceIndex++;
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

  private unsafe void CreateIndirectBuffer(ref Dictionary<uint, IndirectData> pair, ref DwarfBuffer[] buffArray) {
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

  private unsafe void CreateSimpleIndexBuffer(ReadOnlySpan<Node> nodes) {
    _globalSimpleIndexBuffer?.Dispose();

    var adjustedIndices = new List<uint>();
    uint vertexOffset = 0;
    uint indexOffset = 0;

    foreach (var node in nodes) {
      var mesh = node.Mesh;
      if (mesh?.IndexBuffer == null) continue;

      foreach (var idx in mesh.Indices) {
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

  private unsafe void CreateComplexIndexBuffer(ReadOnlySpan<Node> nodes) {
    _globalComplexIndexBuffer?.Dispose();

    var adjustedIndices = new List<uint>();
    uint vertexOffset = 0;
    uint indexOffset = 0;

    foreach (var node in nodes) {
      var mesh = node.Mesh;
      if (mesh?.IndexBuffer == null) continue;

      foreach (var idx in mesh.Indices) {
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
  private unsafe void CreateSimpleVertexBuffer(ReadOnlySpan<Node> nodes) {
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

        AddIndirectCommand(currentPool, node, _vertexSimpleBindings.Last(), ref _indirectSimples, ref _instanceIndexSimple);

        indexOffset += (uint)indices.Length;
        vertexOffset += (uint)verts.Length;

        staging.Dispose();
      }
    }
  }

  private unsafe void CreateComplexVertexBuffer(ReadOnlySpan<Node> nodes) {
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

        AddIndirectCommand(currentPool, node, _vertexComplexBindings.Last(), ref _indirectComplexes, ref _instanceIndexComplex, in _instanceIndexSimple);

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
    _skinnedNodesCache = [];
    _notSkinnedNodesCache = [];
    _cacheMatch = false;
    _skinnedGroups = [];
    _notSkinnedGroups = [];
    _indirectSimples.Clear();
    _indirectComplexes.Clear();
    base.Dispose();
  }
}