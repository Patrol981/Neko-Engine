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
using Neko.Rendering.Renderer2D.Interfaces;
using Neko.Rendering.Renderer2D.Models;
using Neko.Vulkan;
using ZLinq;

namespace Neko.Rendering.Renderer2D;

public class CustomShaderRender2DSystem : SystemBase, IRenderSystem {
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

  private Dictionary<Guid, SpritePushConstant140> _objectDataArray = [];

  public CustomShaderRender2DSystem(
    Application app,
    nint allocator,
    VulkanDevice device,
    IRenderer renderer,
    TextureManager textureManager,
    Dictionary<string, IDescriptorSetLayout> externalLayouts,
    IPipelineConfigInfo configInfo = null!
  ) : base(app, allocator, device, renderer, textureManager, configInfo) {
    _basicLayouts = [
      externalLayouts["Global"],
      externalLayouts["SpriteData"],
      _textureManager.AllTexturesSetLayout,
    ];
  }

  public void Setup(ReadOnlySpan<IDrawable2D> spritesWithCustomShaders) {
    foreach (var sprite in spritesWithCustomShaders) {
      var pipelineName = sprite.CustomShader.Name;

      AddPipelineData(new() {
        RenderPass = _application.Renderer.GetSwapchainRenderPass(),
        VertexName = "sprite_vertex",
        FragmentName = $"{pipelineName}_fragment",
        PipelineProvider = new PipelineSpriteProvider(),
        DescriptorSetLayouts = [.. _basicLayouts],
        PipelineName = pipelineName
      });
    }
  }

  private static int GetIndexOfMyTexture(string texName) {
    var texturePair = Application.Instance.TextureManager.PerSceneLoadedTextures
      .Where(x => x.Value.TextureName == texName)
      .Single();
    return texturePair.Value.TextureManagerIndex;
  }

  public void Update(
    FrameInfo frameInfo,
    ReadOnlySpan<IDrawable2D> spritesWithCustomShaders,
    in ConcurrentDictionary<Guid, Mesh> meshes,
    in HashSet<Entity> entities
  ) {

  }

  private void AddOrUpdateBuffers(
    ReadOnlySpan<IDrawable2D> spritesWithCustomShaders,
    ConcurrentDictionary<Guid, Sprite> meshes
  ) {
    if (LastKnownElemCount == spritesWithCustomShaders.Length) {
      return;
    }
    LastKnownElemCount = spritesWithCustomShaders.Length;

    foreach (var buff in _buffers) {
      buff.VertexBuffer?.Dispose();
      buff.IndexBuffer?.Dispose();
    }
    _buffers.Clear();
    _objectDataArray.Clear();

    foreach (var drawable in spritesWithCustomShaders) {
      CreateIndexBuffer(drawable.Mesh, out var indexBuffer);
      CreateVertexBuffer(drawable.Mesh, out var vertexBuffer);
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
}