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

public class Render3DSystem : SystemBase, IRenderSystem {
  public const string Simple3D = "simple3D";
  public const string Skinned3D = "skinned3D";

  public const string HatchTextureName = "./Resources/T_crossHatching13_D.png";
  public static float HatchScale = 1;

  public bool GlobalVertexBuffer { get; private set; } = false;

  private DwarfBuffer _modelBuffer = null!;
  private DwarfBuffer _complexVertexBuffer = null!;
  private DwarfBuffer _complexIndexBuffer = null!;

  private VkDescriptorSet _dynamicSet = VkDescriptorSet.Null;
  private VulkanDescriptorWriter _dynamicWriter = null!;

  private readonly IDescriptorSetLayout _jointDescriptorLayout = null!;

  // private readonly List<VkDrawIndexedIndirectCommand> _indirectCommands = [];
  // private readonly DwarfBuffer _indirectCommandBuffer = null!;

  // private ModelUniformBufferObject _modelUbo = new();
  private readonly unsafe ModelUniformBufferObject* _modelUbo =
    (ModelUniformBufferObject*)Marshal.AllocHGlobal(Unsafe.SizeOf<ModelUniformBufferObject>());
  private readonly unsafe SimpleModelPushConstant* _pushConstantData =
    (SimpleModelPushConstant*)Marshal.AllocHGlobal(Unsafe.SizeOf<SimpleModelPushConstant>());

  private readonly IRender3DElement[] _notSkinnedEntitiesCache = [];
  private readonly IRender3DElement[] _skinnedEntitiesCache = [];

  private Node[] _notSkinnedNodesCache = [];
  private Node[] _skinnedNodesCache = [];

  private List<(string, int)> _skinnedGroups = [];
  private List<(string, int)> _notSkinnedGroups = [];

  private ITexture _hatchTexture = null!;
  private IDescriptorSetLayout _previousTexturesLayout = null!;

  public Render3DSystem(
    nint allocator,
    IDevice device,
    IRenderer renderer,
    Dictionary<string, IDescriptorSetLayout> externalLayouts,
    VkPipelineConfigInfo configInfo = null!
  ) : base(allocator, device, renderer, configInfo) {
    _setLayout = new VulkanDescriptorSetLayout.Builder(_device)
      .AddBinding(0, DescriptorType.UniformBufferDynamic, ShaderStageFlags.AllGraphics)
      .Build();

    _jointDescriptorLayout = new VulkanDescriptorSetLayout.Builder(_device)
      .AddBinding(0, DescriptorType.UniformBuffer, ShaderStageFlags.AllGraphics)
      .Build();

    _textureSetLayout = new VulkanDescriptorSetLayout.Builder(_device)
      .AddBinding(0, DescriptorType.SampledImage, ShaderStageFlags.Fragment)
      .AddBinding(1, DescriptorType.Sampler, ShaderStageFlags.Fragment)

      // .AddBinding(2, VkDescriptorType.SampledImage, VkShaderStageFlags.Fragment)
      // .AddBinding(3, VkDescriptorType.Sampler, VkShaderStageFlags.Fragment)
      .Build();

    _previousTexturesLayout = new VulkanDescriptorSetLayout.Builder(_device)
      .AddBinding(0, DescriptorType.CombinedImageSampler, ShaderStageFlags.AllGraphics)
      .AddBinding(1, DescriptorType.CombinedImageSampler, ShaderStageFlags.AllGraphics)
      .Build();

    VkDescriptorSetLayout[] basicLayouts = [
      _textureSetLayout.GetDescriptorSetLayoutPointer(),
      _textureSetLayout.GetDescriptorSetLayoutPointer(),
      externalLayouts["Global"].GetDescriptorSetLayoutPointer(),
      externalLayouts["ObjectData"].GetDescriptorSetLayoutPointer(),
      _setLayout.GetDescriptorSetLayoutPointer(),
      externalLayouts["PointLight"].GetDescriptorSetLayoutPointer(),
    ];

    VkDescriptorSetLayout[] complexLayouts = [
      .. basicLayouts,
      externalLayouts["JointsBuffer"].GetDescriptorSetLayoutPointer(),
      // _jointDescriptorLayout.GetDescriptorSetLayout(),
      // _previousTexturesLayout.GetDescriptorSetLayout()
    ];

    AddPipelineData(new() {
      RenderPass = renderer.GetSwapchainRenderPass(),
      VertexName = "vertex",
      FragmentName = "fragment",
      PipelineProvider = new PipelineModelProvider(),
      DescriptorSetLayouts = [.. basicLayouts, _previousTexturesLayout.GetDescriptorSetLayoutPointer()],
      PipelineName = Simple3D
    });

    AddPipelineData(new() {
      RenderPass = renderer.GetSwapchainRenderPass(),
      VertexName = "vertex_skinned",
      FragmentName = "fragment_skinned",
      PipelineProvider = new PipelineModelProvider(),
      DescriptorSetLayouts = [.. complexLayouts, _previousTexturesLayout.GetDescriptorSetLayoutPointer()],
      PipelineName = Skinned3D
    });
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

  private void BuildTargetDescriptorTexture(Entity entity, ref TextureManager textures, int modelPart = 0) {
    var target = entity.GetDrawable<IRender3DElement>() as IRender3DElement;
    var id = target!.GetTextureIdReference(modelPart);
    var texture = textures.GetTextureLocal(id);
    if (texture == null) {
      var nid = textures.GetTextureIdLocal("./Resources/Textures/base/no_texture.png");
      texture = textures.GetTextureLocal(nid);
    }

    texture.BuildDescriptor(_textureSetLayout, _descriptorPool);
  }

  private void BuildTargetDescriptorTexture(IRender3DElement target, ref TextureManager textureManager) {
    for (int i = 0; i < target.MeshedNodesCount; i++) {
      var textureId = target.GetTextureIdReference(i);
      var texture = textureManager.GetTextureLocal(textureId);

      if (texture == null) {
        var nid = textureManager.GetTextureIdLocal("./Resources/Textures/base/no_texture.png");
        texture = textureManager.GetTextureLocal(nid);
      }

      if (texture == null) return;

      texture.BuildDescriptor(_textureSetLayout, _descriptorPool);
    }
  }
  private static int CalculateNodesLength(ReadOnlySpan<Entity> entities) {
    int len = 0;
    foreach (var entity in entities) {
      var i3d = entity.GetDrawable<IRender3DElement>() as IRender3DElement;
      len += i3d!.MeshedNodesCount;
    }
    return len;
  }

  private static (ulong vtxCount, Vertex[] vertices) CalculateVertexCount(ReadOnlySpan<Entity> entities, bool hasSkinFlag) {
    ulong vtxCount = 0;
    List<Vertex> vertices = [];
    foreach (var entity in entities) {
      var i3d = entity.GetDrawable<IRender3DElement>() as IRender3DElement;
      var result = i3d?.MeshedNodes.ToArray();
      for (int i = 0; i < result?.Length; i++) {
        if (result[i].HasSkin == hasSkinFlag) {
          vtxCount += result[i]!.Mesh!.VertexCount;
          vertices.AddRange(result[i]!.Mesh!.Vertices);
        }
      }
    }
    return (vtxCount, [.. vertices]);
  }

  public static (ulong idxCount, uint[] indices) CalculateIndexOffsets(ReadOnlySpan<Entity> entities, bool hasSkinFlag) {
    ulong idxCount = 0;
    List<uint> indices = [];
    foreach (var entity in entities) {
      var i3d = entity.GetDrawable<IRender3DElement>() as IRender3DElement;
      var result = i3d?.MeshedNodes.ToArray();
      for (int i = 0; i < result?.Length; i++) {
        if (result[i].HasSkin == hasSkinFlag) {
          idxCount += result[i]!.Mesh!.IndexCount;
          indices.AddRange(result[i]!.Mesh!.Indices);
        }
      }
    }
    return (idxCount, [.. indices]);
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

  public void Setup(ReadOnlySpan<Entity> entities, ref TextureManager textures) {
    _device.WaitQueue();
    var startTime = DateTime.Now;

    if (entities.Length < 1) {
      Logger.Warn("Entities that are capable of using 3D renderer are less than 1, thus 3D Render System won't be recreated");
      return;
    }

    Logger.Info("Recreating Renderer 3D");

    _descriptorPool = new VulkanDescriptorPool.Builder((VulkanDevice)_device)
      .SetMaxSets(10000)
      .AddPoolSize(DescriptorType.SampledImage, 1000)
      .AddPoolSize(DescriptorType.Sampler, 1000)
      .AddPoolSize(DescriptorType.UniformBufferDynamic, 1000)
      .AddPoolSize(DescriptorType.InputAttachment, 1000)
      .AddPoolSize(DescriptorType.UniformBuffer, 1000)
      .AddPoolSize(DescriptorType.StorageBuffer, 1000)
      .SetPoolFlags(DescriptorPoolCreateFlags.None)
      // .SetPoolFlags(DescriptorPoolCreateFlags.UpdateAfterBind)
      .Build();

    _texturesCount = CalculateLengthOfPool(entities);

    // entities.length before, param no.3
    _modelBuffer = new(
      _allocator,
      _device,
      (ulong)Unsafe.SizeOf<ModelUniformBufferObject>(),
      (ulong)_texturesCount,
      BufferUsage.UniformBuffer,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
      ((VulkanDevice)_device).Properties.limits.minUniformBufferOffsetAlignment
    );

    // LastKnownElemCount = entities.Length;
    LastKnownElemCount = CalculateNodesLength(entities);
    LastKnownElemSize = 0;
    var (len, joints) = CalculateNodesLengthWithSkin(entities);
    LastKnownSkinnedElemCount = (ulong)len;
    LastKnownSkinnedElemJointsCount = (ulong)joints;

    for (int i = 0; i < entities.Length; i++) {
      var targetModel = entities[i].GetDrawable<IRender3DElement>() as IRender3DElement;
      BuildTargetDescriptorTexture(targetModel!, ref textures);
      // BuildTargetDescriptorJointBuffer(targetModel!);

      LastKnownElemSize += targetModel!.CalculateBufferSize();
    }

    var range = _modelBuffer.GetDescriptorBufferInfo(_modelBuffer.GetAlignmentSize());
    range.range = _modelBuffer.GetAlignmentSize();
    unsafe {
      _dynamicWriter = new VulkanDescriptorWriter((VulkanDescriptorSetLayout)_setLayout, (VulkanDescriptorPool)_descriptorPool);
      _dynamicWriter.WriteBuffer(0, &range);
      _dynamicWriter.Build(out _dynamicSet);
    }

    if (_hatchTexture == null) {
      textures.AddTextureGlobal(HatchTextureName).Wait();
      var id = textures.GetTextureIdGlobal(HatchTextureName);
      _hatchTexture = textures.GetTextureGlobal(id);
      _hatchTexture.BuildDescriptor(_textureSetLayout, _descriptorPool);
    }

    CreateGlobalBuffers(entities);

    var endTime = DateTime.Now;
    Logger.Warn($"[RENDER 3D RELOAD TIME]: {(endTime - startTime).TotalMilliseconds}");
  }

  private unsafe void CreateGlobalBuffers(ReadOnlySpan<Entity> entities) {
    if (!GlobalVertexBuffer) return;

    var (vtxCount, vertices) = CalculateVertexCount(entities, hasSkinFlag: true);
    ulong vtxBufferSize = ((ulong)Unsafe.SizeOf<Vertex>()) * vtxCount;

    Logger.Info($"START VTX: {vtxCount}");

    var stagingBuffer = new DwarfBuffer(
      _allocator,
      _device,
      (ulong)Unsafe.SizeOf<Vertex>(),
      vtxCount,
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
      default,
      true
    );

    stagingBuffer.Map(vtxBufferSize);
    fixed (Vertex* verticesPtr = vertices) {
      stagingBuffer.WriteToBuffer((nint)verticesPtr, vtxBufferSize);
    }

    _complexVertexBuffer = new DwarfBuffer(
      _allocator,
      _device,
      (ulong)Unsafe.SizeOf<Vertex>(),
      vtxCount,
      BufferUsage.VertexBuffer | BufferUsage.TransferDst,
      MemoryProperty.DeviceLocal
    );

    _device.CopyBuffer(stagingBuffer.GetBuffer(), _complexVertexBuffer.GetBuffer(), vtxBufferSize);
    stagingBuffer.Dispose();

    var (idxCount, indices) = CalculateIndexOffsets(entities, hasSkinFlag: true);

    stagingBuffer = new DwarfBuffer(
      _allocator,
      _device,
      sizeof(uint),
      idxCount,
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
      default,
      true
    );

    stagingBuffer.Map(sizeof(uint) * idxCount);
    fixed (uint* indicesPtr = indices) {
      stagingBuffer.WriteToBuffer((nint)indicesPtr, sizeof(uint) * idxCount);
    }

    _complexIndexBuffer = new DwarfBuffer(
      _allocator,
      _device,
      sizeof(uint),
      idxCount,
      BufferUsage.IndexBuffer | BufferUsage.TransferDst,
      MemoryProperty.DeviceLocal
    );

    _device.CopyBuffer(stagingBuffer.GetBuffer(), _complexIndexBuffer.GetBuffer(), sizeof(uint) * idxCount);
    stagingBuffer.Dispose();
  }

  public bool CheckSizes(ReadOnlySpan<Entity> entities) {
    if (_modelBuffer == null) {
      var textureManager = Application.Instance.TextureManager;
      Setup(entities, ref textureManager);
    }
    if (entities.Length > (int)_modelBuffer!.GetInstanceCount()) {
      return false;
    } else if (entities.Length < (int)_modelBuffer.GetInstanceCount()) {
      return true;
    }

    return true;
  }

  public bool CheckTextures(ReadOnlySpan<Entity> entities) {
    var len = CalculateLengthOfPool(entities);
    return len == _texturesCount;
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
      if (node.HasSkin) {
        nodeObjectsSkinned.Add(
          new(
            node,
            new ObjectData {
              ModelMatrix = transform.Matrix4,
              NormalMatrix = transform.NormalMatrix,
              NodeMatrix = node.Mesh!.Matrix,
              JointsBufferOffset = new Vector4(offset, 0, 0, 0),
              FilterFlag = node.FilterMeInShader == true ? 1 : 0
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
              FilterFlag = node.FilterMeInShader == true ? 1 : 0
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
  private List<Indirect3DBatch> CreateBatch(List<KeyValuePair<Node, ObjectData>> nodeObjects) {
    List<Indirect3DBatch> batches = [];

    if (nodeObjects.Count == 0) return batches;

    Indirect3DBatch? currentBatch = null;

    foreach (var nodeObject in nodeObjects) {
      var nodeName = nodeObject.Key.Name;

      if (currentBatch == null || currentBatch.Name != nodeName) {
        if (currentBatch != null) {
          currentBatch.Count = (uint)currentBatch.NodeObjects.Count;
          batches.Add(currentBatch);
        }
        currentBatch = new Indirect3DBatch {
          Name = nodeName,
          NodeObjects = []
        };
      }

      currentBatch.NodeObjects.Add(nodeObject);
    }

    if (currentBatch != null) {
      currentBatch.Count = (uint)currentBatch.NodeObjects.Count;
      batches.Add(currentBatch);
    }

    return batches;
  }
  public void Render(FrameInfo frameInfo) {
    PerfMonitor.Clear3DRendererInfo();
    PerfMonitor.NumberOfObjectsRenderedIn3DRenderer = (uint)LastElemRenderedCount;
    if (_notSkinnedNodesCache.Length > 0) {
      RenderSimple(frameInfo, _notSkinnedNodesCache);
    }
    if (_skinnedNodesCache.Length > 0) {
      RenderComplex(frameInfo, _skinnedNodesCache, _notSkinnedNodesCache.Length);
    }
  }

  public void RenderTargets() {
    throw new NotImplementedException();
  }

  private void RenderSimple(FrameInfo frameInfo, Span<Node> nodes) {
    BindPipeline(frameInfo.CommandBuffer, Simple3D);
    unsafe {
      vkCmdBindDescriptorSets(
        frameInfo.CommandBuffer,
        VkPipelineBindPoint.Graphics,
        _pipelines[Simple3D].PipelineLayout,
        2,
        1,
        &frameInfo.GlobalDescriptorSet,
        0,
        null
      );

      vkCmdBindDescriptorSets(
        frameInfo.CommandBuffer,
        VkPipelineBindPoint.Graphics,
        _pipelines[Simple3D].PipelineLayout,
        5,
        1,
        &frameInfo.PointLightsDescriptorSet,
        0,
        null
      );

      vkCmdBindDescriptorSets(
        frameInfo.CommandBuffer,
        VkPipelineBindPoint.Graphics,
        _pipelines[Simple3D].PipelineLayout,
        3,
        1,
        &frameInfo.ObjectDataDescriptorSet,
        0,
        null
      );

      if (_renderer.Swapchain.PreviousFrame != -1) {
        vkCmdBindDescriptorSets(
          frameInfo.CommandBuffer,
          VkPipelineBindPoint.Graphics,
          _pipelines[Simple3D].PipelineLayout,
          6,
          _renderer.PreviousPostProcessDescriptor
        );
      }
    }

    _modelBuffer.Map(_modelBuffer.GetAlignmentSize());
    _modelBuffer.Flush();

    Guid prevTextureId = Guid.Empty;
    Node prevNode = null!;

    for (int i = 0; i < nodes.Length; i++) {
      if (nodes[i].ParentRenderer.GetOwner().CanBeDisposed || !nodes[i].ParentRenderer.GetOwner().Active) continue;
      if (!nodes[i].ParentRenderer.FinishedInitialization) continue;

      var materialData = nodes[i].ParentRenderer.GetOwner().GetComponent<MaterialComponent>().Data;
      unsafe {
        _modelUbo->Color = materialData.Color;
        _modelUbo->Specular = materialData.Specular;
        _modelUbo->Shininess = materialData.Shininess;
        _modelUbo->Diffuse = materialData.Diffuse;
        _modelUbo->Ambient = materialData.Ambient;
      }

      uint dynamicOffset = (uint)_modelBuffer.GetAlignmentSize() * (uint)i;

      unsafe {
        _modelBuffer.WriteToBuffer((IntPtr)_modelUbo, _modelBuffer.GetInstanceSize(), dynamicOffset >> 1);
      }

      unsafe {
        fixed (VkDescriptorSet* descPtr = &_dynamicSet) {
          vkCmdBindDescriptorSets(
            frameInfo.CommandBuffer,
            VkPipelineBindPoint.Graphics,
            _pipelines[Simple3D].PipelineLayout,
            4,
            1,
            descPtr,
            1,
            &dynamicOffset
          );
        }
      }

      if (!nodes[i].ParentRenderer.GetOwner().CanBeDisposed && nodes[i].ParentRenderer.GetOwner().Active) {
        if (i == _texturesCount) continue;
        if (prevTextureId != nodes[i].Mesh!.TextureIdReference) {
          var targetTexture = frameInfo.TextureManager.GetTextureLocal(nodes[i].Mesh!.TextureIdReference);
          VkDescriptorSet[] textureDescriptors = [targetTexture.TextureDescriptor, _hatchTexture.TextureDescriptor];
          Descriptor.BindDescriptorSets(textureDescriptors, frameInfo, PipelineLayout, 0);
          // Descriptor.BindDescriptorSet(
          //   targetTexture.TextureDescriptor,
          //   frameInfo,
          //   _pipelines[Simple3D].PipelineLayout,
          //   0,
          //   1
          // );
          prevTextureId = nodes[i].Mesh!.TextureIdReference;
          PerfMonitor.TextureBindingsIn3DRenderer += 1;
        }

        if (prevNode?.Name != nodes[i].Name || prevNode?.Mesh?.VertexCount != nodes[i]?.Mesh?.VertexCount) {
          nodes[i].BindNode(frameInfo.CommandBuffer);
          PerfMonitor.VertexBindingsIn3DRenderer += 1;
          prevNode = nodes[i];
        }
        nodes[i].DrawNode(frameInfo.CommandBuffer, (uint)i);
      }
    }

    _modelBuffer.Unmap();
  }

  private void RenderComplex(FrameInfo frameInfo, Span<Node> nodes, int prevIdx) {
    BindPipeline(frameInfo.CommandBuffer, Skinned3D);
    unsafe {
      vkCmdBindDescriptorSets(
        frameInfo.CommandBuffer,
        VkPipelineBindPoint.Graphics,
        _pipelines[Skinned3D].PipelineLayout,
        2,
        1,
        &frameInfo.GlobalDescriptorSet,
        0,
        null
      );

      vkCmdBindDescriptorSets(
        frameInfo.CommandBuffer,
        VkPipelineBindPoint.Graphics,
        _pipelines[Skinned3D].PipelineLayout,
        5,
        1,
        &frameInfo.PointLightsDescriptorSet,
        0,
        null
      );

      vkCmdBindDescriptorSets(
        frameInfo.CommandBuffer,
        VkPipelineBindPoint.Graphics,
        _pipelines[Skinned3D].PipelineLayout,
        3,
        1,
        &frameInfo.ObjectDataDescriptorSet,
        0,
        null
      );

      vkCmdBindDescriptorSets(
        frameInfo.CommandBuffer,
        VkPipelineBindPoint.Graphics,
        _pipelines[Skinned3D].PipelineLayout,
        6,
        1,
        &frameInfo.JointsBufferDescriptorSet,
        0,
        null
      );

      if (_renderer.Swapchain.PreviousFrame != -1) {
        vkCmdBindDescriptorSets(
          frameInfo.CommandBuffer,
          VkPipelineBindPoint.Graphics,
          _pipelines[Skinned3D].PipelineLayout,
          7,
          _renderer.PreviousPostProcessDescriptor
        );
      }
    }

    _modelBuffer.Map(_modelBuffer.GetAlignmentSize());
    _modelBuffer.Flush();

    ulong vtxOffset = 0;
    ulong idxOffset = 0;

    Guid prevTextureId = Guid.Empty;
    Node? prevNode = null;

    for (int i = 0; i < nodes.Length; i++) {
      if (nodes[i].ParentRenderer.GetOwner().CanBeDisposed || !nodes[i].ParentRenderer.GetOwner().Active) continue;
      if (!nodes[i].ParentRenderer.FinishedInitialization) continue;

      var materialData = nodes[i].ParentRenderer.GetOwner().GetComponent<MaterialComponent>().Data;
      unsafe {
        _modelUbo->Color = materialData.Color;
        _modelUbo->Specular = materialData.Specular;
        _modelUbo->Shininess = materialData.Shininess;
        _modelUbo->Diffuse = materialData.Diffuse;
        _modelUbo->Ambient = materialData.Ambient;
      }

      uint dynamicOffset = (uint)_modelBuffer.GetAlignmentSize() * ((uint)i + (uint)prevIdx);

      unsafe {
        _modelBuffer.WriteToBuffer((IntPtr)(_modelUbo), _modelBuffer.GetInstanceSize(), dynamicOffset >> 1);
      }

      unsafe {
        fixed (VkDescriptorSet* descPtr = &_dynamicSet) {
          vkCmdBindDescriptorSets(
            frameInfo.CommandBuffer,
            VkPipelineBindPoint.Graphics,
            _pipelines[Skinned3D].PipelineLayout,
            4,
            1,
            descPtr,
            1,
            &dynamicOffset
          );
        }
      }

      if (i <= nodes.Length && nodes[i].ParentRenderer.Animations.Count > 0) {
        nodes[i].ParentRenderer.GetOwner().GetComponent<AnimationController>().Update(nodes[i]);
      }

      if (prevTextureId != nodes[i].Mesh!.TextureIdReference) {
        var targetTexture = frameInfo.TextureManager.GetTextureLocal(nodes[i].Mesh!.TextureIdReference);
        VkDescriptorSet[] textureDescriptors = [targetTexture.TextureDescriptor, _hatchTexture.TextureDescriptor];
        // Descriptor.BindDescriptorSet(targetTexture.TextureDescriptor, frameInfo, PipelineLayout, 0, 1);
        Descriptor.BindDescriptorSets(textureDescriptors, frameInfo, PipelineLayout, 0);
        prevTextureId = nodes[i].Mesh!.TextureIdReference;
        PerfMonitor.TextureBindingsIn3DRenderer += 1;
      }

      if (GlobalVertexBuffer) {
        nodes[i].BindNode(frameInfo.CommandBuffer, _complexVertexBuffer, _complexIndexBuffer, vtxOffset, idxOffset);
        nodes[i].DrawNode(frameInfo.CommandBuffer, (int)vtxOffset, (uint)i + (uint)prevIdx);

        vtxOffset += nodes[i].Mesh!.VertexCount * (ulong)Unsafe.SizeOf<Vertex>();
        idxOffset += nodes[i].Mesh!.IndexCount * sizeof(uint);
      } else {
        if (prevNode?.Name != nodes[i].Name || prevNode?.Mesh?.VertexCount != nodes[i]?.Mesh?.VertexCount) {
          nodes[i].BindNode(frameInfo.CommandBuffer);
          PerfMonitor.VertexBindingsIn3DRenderer += 1;
          prevNode = nodes[i];
        }
        nodes[i].DrawNode(frameInfo.CommandBuffer, (uint)i + (uint)prevIdx);
      }
    }

    _modelBuffer.Unmap();
  }


  public override unsafe void Dispose() {
    _device.WaitQueue();
    _device.WaitDevice();

    MemoryUtils.FreeIntPtr<SimpleModelPushConstant>((nint)_pushConstantData);
    MemoryUtils.FreeIntPtr<ModelUniformBufferObject>((nint)_modelUbo);

    _complexVertexBuffer?.Dispose();
    _complexIndexBuffer?.Dispose();

    _modelBuffer?.Dispose();
    // _descriptorPool?.FreeDescriptors([_dynamicSet]);

    _jointDescriptorLayout?.Dispose();
    _previousTexturesLayout?.Dispose();

    base.Dispose();
  }

  public IRender3DElement[] CachedRenderables => [.. _notSkinnedEntitiesCache, .. _skinnedEntitiesCache];
  public int LastKnownElemCount { get; private set; }
  public int LastElemRenderedCount => _notSkinnedNodesCache.Length + _skinnedNodesCache.Length;
  public ulong LastKnownElemSize { get; private set; }
  public ulong LastKnownSkinnedElemCount { get; private set; }
  public ulong LastKnownSkinnedElemJointsCount { get; private set; }
}