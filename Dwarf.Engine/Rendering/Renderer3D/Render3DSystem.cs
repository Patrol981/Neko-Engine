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

  public int LastElemRenderedCount => _notSkinnedNodesCache.Length + _skinnedNodesCache.Length;
  public int LastKnownElemCount { get; private set; } = 0;
  public ulong LastKnownElemSize { get; private set; } = 0;
  public ulong LastKnownSkinnedElemCount { get; private set; }
  public ulong LastKnownSkinnedElemJointsCount { get; private set; }

  private DwarfBuffer? _globalIndexBuffer;
  private DwarfBuffer? _globalVertexBuffer;
  private DwarfBuffer? _indirectBuffer;

  private readonly IDescriptorSetLayout _jointDescriptorLayout = null!;

  private List<VkDrawIndexedIndirectCommand> _indirectDrawCommands = [];

  private Node[] _notSkinnedNodesCache = [];
  private Node[] _skinnedNodesCache = [];

  private List<(string, int)> _skinnedGroups = [];
  private List<(string, int)> _notSkinnedGroups = [];

  private ITexture _hatchTexture = null!;
  private IDescriptorSetLayout _previousTexturesLayout = null!;

  private BufferPool _bufferPool = null!;

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

    CreateVertexBuffer(entities);
    CreateIndexBuffer(entities);

    CreateIndirectCommands(entities);
    CreateIndirectBuffer();
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

  public unsafe void RenderSimple(FrameInfo frameInfo, Span<Node> nodes) {
    BindPipeline(frameInfo.CommandBuffer, Simple3D);

    Descriptor.BindDescriptorSet(_textureManager.AllTexturesDescriptor, frameInfo, _pipelines[Simple3D].PipelineLayout, 0, 1);
    Descriptor.BindDescriptorSet(frameInfo.GlobalDescriptorSet, frameInfo, _pipelines[Simple3D].PipelineLayout, 1, 1);
    Descriptor.BindDescriptorSet(frameInfo.ObjectDataDescriptorSet, frameInfo, _pipelines[Simple3D].PipelineLayout, 2, 1);
    Descriptor.BindDescriptorSet(frameInfo.PointLightsDescriptorSet, frameInfo, _pipelines[Simple3D].PipelineLayout, 3, 1);

    ulong lastVtxCount = 0;

    for (int i = 0; i < nodes.Length; i++) {
      if (nodes[i].ParentRenderer.GetOwner().CanBeDisposed || !nodes[i].ParentRenderer.GetOwner().Active) continue;
      if (!nodes[i].ParentRenderer.FinishedInitialization) continue;

      if (lastVtxCount != nodes[i].Mesh!.VertexCount) {
        nodes[i].BindNode(frameInfo.CommandBuffer);
        lastVtxCount = nodes[i].Mesh!.VertexCount;
      }
      nodes[i].DrawNode(frameInfo.CommandBuffer, (uint)i);
    }
  }

  public unsafe void RenderComplex(FrameInfo frameInfo, Span<Node> nodes, int offset) {
    BindPipeline(frameInfo.CommandBuffer, Skinned3D);

    Descriptor.BindDescriptorSet(_textureManager.AllTexturesDescriptor, frameInfo, _pipelines[Skinned3D].PipelineLayout, 0, 1);
    Descriptor.BindDescriptorSet(frameInfo.GlobalDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 1, 1);
    Descriptor.BindDescriptorSet(frameInfo.ObjectDataDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 2, 1);
    Descriptor.BindDescriptorSet(frameInfo.PointLightsDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 3, 1);
    Descriptor.BindDescriptorSet(frameInfo.JointsBufferDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 4, 1);

    ulong lastVtxCount = 0;

    for (int i = 0; i < nodes.Length; i++) {
      if (nodes[i].ParentRenderer.GetOwner().CanBeDisposed || !nodes[i].ParentRenderer.GetOwner().Active) continue;
      if (!nodes[i].ParentRenderer.FinishedInitialization) continue;

      if (i <= nodes.Length && nodes[i].ParentRenderer.Animations.Count > 0) {
        nodes[i].ParentRenderer.GetOwner().TryGetComponent<AnimationController>()?.Update(nodes[i]);
      }

      if (lastVtxCount != nodes[i].Mesh!.VertexCount) {
        nodes[i].BindNode(frameInfo.CommandBuffer);
        lastVtxCount = nodes[i].Mesh!.VertexCount;
      }

      nodes[i].DrawNode(frameInfo.CommandBuffer, (uint)i + (uint)offset);
    }
  }

  public unsafe void Render_(FrameInfo frameInfo) {
    BindPipeline(frameInfo.CommandBuffer, Skinned3D);

    Descriptor.BindDescriptorSet(_textureManager.AllTexturesDescriptor, frameInfo, _pipelines[Skinned3D].PipelineLayout, 0, 1);
    Descriptor.BindDescriptorSet(frameInfo.GlobalDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 1, 1);
    Descriptor.BindDescriptorSet(frameInfo.ObjectDataDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 2, 1);
    Descriptor.BindDescriptorSet(frameInfo.PointLightsDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 3, 1);
    Descriptor.BindDescriptorSet(frameInfo.JointsBufferDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 4, 1);

    // _renderer.CommandList.BindVertex(frameInfo.CommandBuffer, _globalVertexBuffer, 0);
    _renderer.CommandList.BindIndex(frameInfo.CommandBuffer, _globalIndexBuffer!, 0);
    _renderer.CommandList.DrawIndexedIndirect(
      frameInfo.CommandBuffer,
      _indirectBuffer!.GetBuffer(),
      0,
      (uint)_indirectDrawCommands.Count,
      (uint)Unsafe.SizeOf<VkDrawIndexedIndirectCommand>()
    );

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
    _indirectBuffer?.Dispose();

    var size = _indirectDrawCommands.Count * Unsafe.SizeOf<VkDrawIndexedIndirectCommand>();

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
    fixed (VkDrawIndexedIndirectCommand* pIndirectCommands = _indirectDrawCommands.ToArray()) {
      stagingBuffer.WriteToBuffer((nint)pIndirectCommands, (ulong)size);
    }

    _indirectBuffer = new DwarfBuffer(
      _allocator,
      _device,
      (ulong)size,
      BufferUsage.TransferDst | BufferUsage.IndirectBuffer,
      MemoryProperty.DeviceLocal
    );

    _device.CopyBuffer(stagingBuffer.GetBuffer(), _indirectBuffer.GetBuffer(), (ulong)size);
    stagingBuffer.Dispose();
  }

  private unsafe void CreateIndexBuffer(ReadOnlySpan<Entity> drawables) {
    _globalIndexBuffer?.Dispose();

    var adjustedIndices = new List<uint>();
    uint vertexOffset = 0;

    for (int i = 0; i < drawables.Length; i++) {
      foreach (var node in drawables[i].GetComponent<MeshRenderer>().MeshedNodes) {
        var mesh = node.Mesh;
        if (mesh?.IndexBuffer == null) continue;

        foreach (var idx in mesh.Indices) {
          adjustedIndices.Add(idx + vertexOffset);
        }

        vertexOffset += (uint)mesh.Vertices.Length;
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

  private unsafe void CreateVertexBuffer(ReadOnlySpan<Entity> drawables) {
    for (int i = 0; i < drawables.Length; i++) {

    }
  }

  private unsafe void CreateVertexBuffer_(ReadOnlySpan<Entity> drawables) {
    _globalVertexBuffer?.Dispose();
    List<Vertex> vertices = [];

    for (int i = 0; i < drawables.Length; i++) {
      foreach (var node in drawables[i].GetComponent<MeshRenderer>().MeshedNodes) {
        var buffer = node.Mesh?.VertexBuffer;
        if (buffer != null) {
          vertices.AddRange(node.Mesh!.Vertices);
        }
      }
    }

    var vtxSize = (ulong)vertices.Count * (ulong)Unsafe.SizeOf<Vertex>();

    var stagingBuffer = new DwarfBuffer(
      _allocator,
      _device,
      vtxSize,
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
      stagingBuffer: true,
      cpuAccessible: true
    );

    stagingBuffer.Map(vtxSize);
    fixed (Vertex* pVertices = vertices.ToArray()) {
      stagingBuffer.WriteToBuffer((nint)pVertices, vtxSize);
    }

    _globalVertexBuffer = new DwarfBuffer(
      _allocator,
      _device,
      vtxSize,
      (ulong)vertices.Count,
      BufferUsage.VertexBuffer | BufferUsage.TransferDst,
      MemoryProperty.DeviceLocal
    );

    _device.CopyBuffer(stagingBuffer.GetBuffer(), _globalVertexBuffer.GetBuffer(), vtxSize);
    stagingBuffer.Dispose();
  }

  public override unsafe void Dispose() {
    _device.WaitQueue();
    _globalVertexBuffer?.Dispose();
    _globalIndexBuffer?.Dispose();
    _indirectBuffer?.Dispose();
    base.Dispose();
  }
}