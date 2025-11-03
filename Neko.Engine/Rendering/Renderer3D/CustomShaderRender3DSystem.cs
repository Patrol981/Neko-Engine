using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Neko;
using Neko.AbstractionLayer;
using Neko.EntityComponentSystem;
using Neko.Extensions.Logging;
using Neko.Globals;
using Neko.Rendering;
using Neko.Vulkan;
using ZLinq;

namespace Neko.Rendering.Renderer3D;

public class CustomShaderRender3DSystem : SystemBase, IRenderSystem {
  private struct CustomShaderBuffer {
    public required Guid EntityId { get; set; }
    public required Guid BufferId { get; set; }
    public required Guid MeshId { get; set; }

    public NekoBuffer VertexBuffer { get; set; }
    public NekoBuffer IndexBuffer { get; set; }

    public required uint IndexCount { get; set; }

    public required string PipelineName { get; set; }

    public Guid ShaderTextureId { get; set; }
  };

  private readonly IDescriptorSetLayout[] _basicLayouts = [];

  public int LastKnownElemCount { get; set; } = 0;
  private List<CustomShaderBuffer> _buffers = [];
  private Dictionary<Guid, ObjectData> _objectDataArray = [];

  public CustomShaderRender3DSystem(
    Application app,
    nint allocator,
    VulkanDevice device,
    IRenderer renderer,
    TextureManager textureManager,
    Dictionary<string, IDescriptorSetLayout> externalLayouts,
    IPipelineConfigInfo configInfo = null!
  ) : base(app, allocator, device, renderer, textureManager, configInfo) {
    _basicLayouts = [
      _textureManager.AllTexturesSetLayout,
      externalLayouts["Global"],
      externalLayouts["CustomShaderObjectData"],
      externalLayouts["PointLight"],
    ];
  }

  public void Setup(ReadOnlySpan<IRender3DElement> renderablesWithCustomShaders) {
    foreach (var renderable in renderablesWithCustomShaders) {
      var pipelineName = renderable.CustomShader.Name;

      AddPipelineData(new() {
        RenderPass = _application.Renderer.GetSwapchainRenderPass(),
        VertexName = "model_vertex",
        FragmentName = $"{pipelineName}_fragment",
        GeometryName = "model_geometry",
        PipelineProvider = new PipelineModelProvider(),
        DescriptorSetLayouts = [.. _basicLayouts],
        PipelineName = pipelineName
      });
    }
  }

  private static int GetIndexOfMyTexture(string texName) {
    var texturePair = Application.Instance.TextureManager
      .PerSceneLoadedTextures.Where(x => x.Value.TextureName == texName)
      .Single();
    return texturePair.Value.TextureManagerIndex;
  }

  [MethodImpl(MethodImplOptions.AggressiveOptimization)]
  public void Update(
    FrameInfo frameInfo,
    ReadOnlySpan<IRender3DElement> renderablesWithCustomShaders,
    in ConcurrentDictionary<Guid, Mesh> meshes,
    in HashSet<Entity> entities
  ) {
    AddOrUpdateBuffers(renderablesWithCustomShaders, meshes);

    for (ushort i = 0; i < _buffers.Count; i++) {
      var entity = entities.Where(x => x.Id == _buffers[i].EntityId).First();
      var transform = entity.GetTransform();
      var mesh = meshes[_buffers[i].MeshId];
      var material = entity.GetMaterial();

      var texture = _textureManager.GetTextureLocal(mesh.TextureIdReference);
      var shaderTexture = _textureManager.GetTextureLocal(_buffers[i].ShaderTextureId);

      var texId = GetIndexOfMyTexture(texture.TextureName);
      int shaderTextureId = 0;
      if (shaderTexture is not null) {
        shaderTextureId = GetIndexOfMyTexture(shaderTexture.TextureName);
      } else {
        Logger.Warn($"shader texture returned null [{_buffers[i].ShaderTextureId}]");
      }

      ref ObjectData objectData = ref CollectionsMarshal.GetValueRefOrAddDefault(
        _objectDataArray,
        _buffers[i].BufferId,
        out var exists
      );

      if (!exists) continue;

      objectData.ModelMatrix = transform?.Matrix() ?? Matrix4x4.Identity;
      objectData.NormalMatrix = transform?.NormalMatrix() ?? Matrix4x4.Identity;
      objectData.NodeMatrix = mesh.Matrix;
      objectData.JointsBufferOffset = Vector4.Zero;
      objectData.AmbientAndTexId0.W = texId;
      objectData.DiffuseAndTexId1.W = shaderTextureId;

      if (objectData.DiffuseAndTexId1.X >= 60.0f) {
        objectData.DiffuseAndTexId1.X = 0.0f;
      }
      objectData.DiffuseAndTexId1.X += Time.DeltaTime;
    }

    unsafe {
      fixed (ObjectData* pObjectData = _objectDataArray.Values.AsValueEnumerable().ToArray()) {
        _application.StorageCollection.WriteBuffer(
          "CustomShaderObjectStorage",
          frameInfo.FrameIndex,
          (nint)pObjectData,
          (ulong)Unsafe.SizeOf<ObjectData>() * (ulong)_objectDataArray.Values.Count
        );
      }
    }
  }

  public void Render(FrameInfo frameInfo) {
    string currentPipelineName = "";
    foreach (var buffer in _buffers) {
      if (currentPipelineName != buffer.PipelineName) {
        currentPipelineName = buffer.PipelineName;
        BindPipeline(frameInfo.CommandBuffer, buffer.PipelineName);

        Descriptor.BindDescriptorSet(_device, _textureManager.AllTexturesDescriptor, frameInfo, _pipelines[currentPipelineName].PipelineLayout, 0, 1);
        Descriptor.BindDescriptorSet(_device, frameInfo.GlobalDescriptorSet, frameInfo, _pipelines[currentPipelineName].PipelineLayout, 1, 1);
        Descriptor.BindDescriptorSet(_device, frameInfo.CustomShaderObjectDataDescriptorSet, frameInfo, _pipelines[currentPipelineName].PipelineLayout, 2, 1);
        Descriptor.BindDescriptorSet(_device, frameInfo.PointLightsDescriptorSet, frameInfo, _pipelines[currentPipelineName].PipelineLayout, 3, 1);
      }

      _renderer.CommandList.BindIndex(frameInfo.CommandBuffer, buffer.IndexBuffer, 0);
      _renderer.CommandList.BindVertex(frameInfo.CommandBuffer, buffer.VertexBuffer, 0);

      _renderer.CommandList.DrawIndexed(
        commandBuffer: frameInfo.CommandBuffer,
        indexCount: buffer.IndexCount,
        instanceCount: 1,
        firstIndex: 0,
        vertexOffset: 0,
        firstInstance: 0
      );
    }
  }

  private void AddOrUpdateBuffers(
    ReadOnlySpan<IRender3DElement> renderablesWithCustomShaders,
    ConcurrentDictionary<Guid, Mesh> meshes
  ) {
    if (LastKnownElemCount == renderablesWithCustomShaders.Length) {
      return;
    }
    LastKnownElemCount = renderablesWithCustomShaders.Length;

    foreach (var buff in _buffers) {
      buff.VertexBuffer?.Dispose();
      buff.IndexBuffer?.Dispose();
    }
    _buffers.Clear();
    _objectDataArray.Clear();

    foreach (var renderable in renderablesWithCustomShaders) {
      foreach (var meshNode in renderable.MeshedNodes) {
        if (meshNode.HasSkin) {
          throw new NotSupportedException("This system does not support skinned meshes");
        }

        CreateIndexBuffer(meshes[meshNode.MeshGuid], out var indexBuffer);
        CreateVertexBuffer(meshes[meshNode.MeshGuid], out var vertexBuffer);

        var bufferId = Guid.NewGuid();

        _buffers.Add(new CustomShaderBuffer() {
          EntityId = renderable.Owner.Id,
          BufferId = bufferId,
          MeshId = meshNode.MeshGuid,
          VertexBuffer = vertexBuffer,
          IndexBuffer = indexBuffer,
          IndexCount = (uint)meshes[meshNode.MeshGuid].IndexCount,
          PipelineName = renderable.CustomShader.Name,
          ShaderTextureId = renderable.CustomShader.ShaderTextureId
        });

        _objectDataArray.TryAdd(bufferId, default);
      }
    }
  }

  private unsafe void CreateVertexBuffer(in Mesh mesh, out NekoBuffer vertexBuffer) {
    var byteSize = (ulong)Unsafe.SizeOf<Vertex>() * mesh.VertexCount;

    var stagingBuffer = new NekoBuffer(
      _allocator,
      _device,
      byteSize,
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
      stagingBuffer: true,
      cpuAccessible: true
    );

    stagingBuffer.Map();
    fixed (Vertex* vertexData = mesh.Vertices) {
      stagingBuffer.WriteToBuffer((nint)vertexData, byteSize);
    }
    stagingBuffer.Unmap();

    vertexBuffer = new NekoBuffer(
      _allocator,
      _device,
      byteSize,
      BufferUsage.TransferDst | BufferUsage.VertexBuffer,
      MemoryProperty.DeviceLocal
    );

    _device.CopyBuffer(stagingBuffer.GetBuffer(), vertexBuffer.GetBuffer(), stagingBuffer.GetBufferSize());
    stagingBuffer.Dispose();
  }

  private unsafe void CreateIndexBuffer(in Mesh mesh, out NekoBuffer indexBuffer) {
    var byteSize = sizeof(uint) * mesh.IndexCount;

    var stagingBuffer = new NekoBuffer(
      _allocator,
      _device,
      byteSize,
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
      stagingBuffer: true,
      cpuAccessible: true
    );

    stagingBuffer.Map(byteSize);
    fixed (uint* indexData = mesh.Indices) {
      stagingBuffer.WriteToBuffer((nint)indexData, byteSize);
    }
    stagingBuffer.Unmap();

    indexBuffer = new NekoBuffer(
      _allocator,
      _device,
      (ulong)Unsafe.SizeOf<uint>(),
      mesh.IndexCount,
      BufferUsage.IndexBuffer | BufferUsage.TransferDst,
      MemoryProperty.DeviceLocal
    );

    _device.CopyBuffer(
      stagingBuffer.GetBuffer(),
      indexBuffer.GetBuffer(),
      byteSize
    );
    stagingBuffer.Dispose();

  }

  public override void Dispose() {
    foreach (var buff in _buffers) {
      buff.VertexBuffer?.Dispose();
      buff.IndexBuffer?.Dispose();
    }
    base.Dispose();
  }
}