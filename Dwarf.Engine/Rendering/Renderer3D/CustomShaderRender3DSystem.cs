using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Dwarf;
using Dwarf.AbstractionLayer;
using Dwarf.Rendering;
using Dwarf.Vulkan;

namespace Dwarf.Rendering.Renderer3D;

public class CustomShaderRender3DSystem : SystemBase, IRenderSystem {
  private struct CustomShaderBuffer {
    private DwarfBuffer VertexBuffer { get; set; }
    private DwarfBuffer IndexBuffer { get; set; }
  };

  private readonly IDescriptorSetLayout[] _basicLayouts = [];

  public int LastKnownElemCount { get; set; } = 0;
  private readonly Dictionary<Guid, CustomShaderBuffer> _buffers = [];

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



      // _buffers.Add(renderable.Owner.Id, )
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveOptimization)]
  public void Update(FrameInfo frameInfo, ConcurrentDictionary<Guid, Mesh> meshes) {

  }

  public void Render() {

  }

  private unsafe void CreateVertexBuffer(in Mesh mesh, out DwarfBuffer vertexBuffer) {
    var byteSize = (ulong)Unsafe.SizeOf<Vertex>() * mesh.VertexCount;

    var stagingBuffer = new DwarfBuffer(
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

    vertexBuffer = new DwarfBuffer(
      _allocator,
      _device,
      byteSize,
      BufferUsage.TransferDst | BufferUsage.VertexBuffer,
      MemoryProperty.DeviceLocal
    );

    _device.CopyBuffer(stagingBuffer.GetBuffer(), vertexBuffer.GetBuffer(), stagingBuffer.GetBufferSize());
    stagingBuffer.Dispose();
  }

  private unsafe void CreateIndexBuffer(in Mesh mesh, out DwarfBuffer indexBuffer) {
    var byteSize = sizeof(uint) * mesh.IndexCount;

    var stagingBuffer = new DwarfBuffer(
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

    indexBuffer = new DwarfBuffer(
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
    base.Dispose();
  }
}