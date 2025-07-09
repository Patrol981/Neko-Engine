using System.Numerics;
using System.Runtime.CompilerServices;

using Dwarf.AbstractionLayer;
using Dwarf.Vulkan;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Rendering;

public class Skybox : IDisposable {
  protected class SkyboxMesh {
    public TexturedVertex[] Vertices = [];
  }

  private readonly VulkanDevice _device;
  private readonly nint _allocator;
  private readonly TextureManager _textureManager;
  private readonly IRenderer _renderer;
  private readonly float[] _vertices = [
    // positions
    -1.0f,
    1.0f,
    -1.0f,
    -1.0f,
    -1.0f,
    -1.0f,
    1.0f,
    -1.0f,
    -1.0f,
    1.0f,
    -1.0f,
    -1.0f,
    1.0f,
    1.0f,
    -1.0f,
    -1.0f,
    1.0f,
    -1.0f,

    -1.0f,
    -1.0f,
    1.0f,
    -1.0f,
    -1.0f,
    -1.0f,
    -1.0f,
    1.0f,
    -1.0f,
    -1.0f,
    1.0f,
    -1.0f,
    -1.0f,
    1.0f,
    1.0f,
    -1.0f,
    -1.0f,
    1.0f,

    1.0f,
    -1.0f,
    -1.0f,
    1.0f,
    -1.0f,
    1.0f,
    1.0f,
    1.0f,
    1.0f,
    1.0f,
    1.0f,
    1.0f,
    1.0f,
    1.0f,
    -1.0f,
    1.0f,
    -1.0f,
    -1.0f,

    -1.0f,
    -1.0f,
    1.0f,
    -1.0f,
    1.0f,
    1.0f,
    1.0f,
    1.0f,
    1.0f,
    1.0f,
    1.0f,
    1.0f,
    1.0f,
    -1.0f,
    1.0f,
    -1.0f,
    -1.0f,
    1.0f,

    -1.0f,
    1.0f,
    -1.0f,
    1.0f,
    1.0f,
    -1.0f,
    1.0f,
    1.0f,
    1.0f,
    1.0f,
    1.0f,
    1.0f,
    -1.0f,
    1.0f,
    1.0f,
    -1.0f,
    1.0f,
    -1.0f,

    -1.0f,
    -1.0f,
    -1.0f,
    -1.0f,
    -1.0f,
    1.0f,
    1.0f,
    -1.0f,
    -1.0f,
    1.0f,
    -1.0f,
    -1.0f,
    -1.0f,
    -1.0f,
    1.0f,
    1.0f,
    -1.0f,
    1.0f
  ];

  private readonly float[] _uvs = [
    // Front
    0.0f,
    1.0f,
    0.0f,
    0.0f,
    1.0f,
    0.0f,
    1.0f,
    0.0f,
    1.0f,
    1.0f,
    0.0f,
    1.0f,

    // Left
    1.0f,
    0.0f,
    0.0f,
    0.0f,
    0.0f,
    1.0f,
    0.0f,
    1.0f,
    1.0f,
    1.0f,
    1.0f,
    0.0f,

    // Right
    0.0f,
    0.0f,
    1.0f,
    0.0f,
    1.0f,
    1.0f,
    1.0f,
    1.0f,
    0.0f,
    1.0f,
    0.0f,
    0.0f,

    // Back
    0.0f,
    1.0f,
    0.0f,
    0.0f,
    1.0f,
    0.0f,
    1.0f,
    0.0f,
    1.0f,
    1.0f,
    0.0f,
    1.0f,

    // Top
    0.0f,
    1.0f,
    1.0f,
    1.0f,
    1.0f,
    0.0f,
    1.0f,
    0.0f,
    0.0f,
    0.0f,
    0.0f,
    1.0f,

    // Bottom
    0.0f,
    0.0f,
    0.0f,
    1.0f,
    1.0f,
    0.0f,
    1.0f,
    0.0f,
    0.0f,
    1.0f,
    1.0f,
    1.0f
  ];
  private readonly SkyboxMesh _mesh;
  private readonly Transform _transform;
  private readonly MaterialComponent _material;

  private VkPipelineConfigInfo _pipelineConfigInfo = null!;
  private VkPipelineLayout _pipelineLayout;
  private VulkanPipeline _pipeline = null!;

  private VulkanDescriptorPool _descriptorPool = null!;
  private VulkanDescriptorPool _texturePool = null!;
  private readonly VulkanDescriptorSetLayout _textureSetLayout = null!;

  private VkDescriptorSet _textureSet = VkDescriptorSet.Null;
  private DwarfBuffer _skyboxBuffer = null!;
  private DwarfBuffer _vertexBuffer = null!;
  private ulong _vertexCount = 0;

  private readonly string[] _cubemapNames = new string[6];
  private CubeMapTexture _cubemapTexture = null!;

  public Skybox(nint allocator, VulkanDevice device, TextureManager textureManager, IRenderer renderer, VkDescriptorSetLayout globalSetLayout) {
    _allocator = allocator;
    _device = device;
    _textureManager = textureManager;
    _renderer = renderer;
    _transform = new();
    _material = new(new Vector3(1.0f, 1.0f, 1.0f));

    _textureSetLayout = new VulkanDescriptorSetLayout.Builder(_device)
    .AddBinding(0, DescriptorType.CombinedImageSampler, ShaderStageFlags.Fragment)
    .Build();

    var meshVertices = new List<TexturedVertex>();
    for (int i = 0, j = 0; i < _vertices.Length; i += 3, j += 2) {
      meshVertices.Add(new() {
        Position = new(_vertices[i], _vertices[i + 1], _vertices[i + 2]),
        Uv = new(_uvs[j], _uvs[j + 1])
      });
    }

    _mesh = new() {
      Vertices = [.. meshVertices]
    };

    var dir = Dwarf.Utils.DwarfPath.AssemblyDirectory;
    _cubemapNames[0] = $"{dir}/Resources/skyboxes/sunny/right.jpg";
    _cubemapNames[1] = $"{dir}/Resources/skyboxes/sunny/left.jpg";
    _cubemapNames[2] = $"{dir}/Resources/skyboxes/sunny/bottom.jpg";
    _cubemapNames[3] = $"{dir}/Resources/skyboxes/sunny/top.jpg";
    _cubemapNames[4] = $"{dir}/Resources/skyboxes/sunny/front.jpg";
    _cubemapNames[5] = $"{dir}/Resources/skyboxes/sunny/back.jpg";

    VkDescriptorSetLayout[] descriptorSetLayouts = [
      _textureSetLayout.GetDescriptorSetLayout(),
      globalSetLayout,
    ];

    CreatePipelineLayout(descriptorSetLayouts);
    CreatePipeline(VkRenderPass.Null, "skybox_vertex", "skybox_fragment", new PipelineSkyboxProvider());

    InitCubeMapTexture();
  }

  private async void InitCubeMapTexture() {
    var data = await CubeMapTexture.LoadDataFromPath(_cubemapNames[0]);
    _cubemapTexture = new CubeMapTexture(_allocator, _device, data.Width, data.Height, _cubemapNames, "cubemap0");

    CreateVertexBuffer(_mesh.Vertices);
    CreateBuffers();
    BindDescriptorTexture();
  }

  public unsafe void Render(FrameInfo frameInfo) {
    _pipeline.Bind(frameInfo.CommandBuffer);

    vkCmdBindDescriptorSets(
      frameInfo.CommandBuffer,
      VkPipelineBindPoint.Graphics,
      _pipelineLayout,
      1,
      1,
      &frameInfo.GlobalDescriptorSet,
      0,
      null
    );

    var pushConstantData = new SkyboxBufferObject {
      SkyboxMatrix = _transform.Matrix4,
      SkyboxColor = _material.Color
    };

    vkCmdPushConstants(
      frameInfo.CommandBuffer,
      _pipelineLayout,
      VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment,
      0,
      (uint)Unsafe.SizeOf<SkyboxBufferObject>(),
       &pushConstantData
    );

    Bind(frameInfo.CommandBuffer);
    BindTexture(frameInfo);
    Draw(frameInfo.CommandBuffer, (uint)_vertexCount);
  }

  private unsafe void BindTexture(FrameInfo frameInfo) {
    Descriptor.BindDescriptorSet(_textureSet, frameInfo, _pipelineLayout, 0, 1);
  }

  private unsafe void BindDescriptorTexture() {
    VkDescriptorImageInfo imageInfo = new() {
      sampler = _cubemapTexture.Sampler,
      imageLayout = VkImageLayout.ShaderReadOnlyOptimal,
      imageView = _cubemapTexture.ImageView
    };
    _ = new VulkanDescriptorWriter(_textureSetLayout, _texturePool)
      .WriteImage(0, &imageInfo)
      .Build(out VkDescriptorSet set);
    _textureSet = set;
  }

  private unsafe void Bind(VkCommandBuffer commandBuffer) {
    VkBuffer[] buffers = [_vertexBuffer.GetBuffer()];
    ulong[] offsets = [0];
    fixed (VkBuffer* buffersPtr = buffers)
    fixed (ulong* offsetsPtr = offsets) {
      vkCmdBindVertexBuffers(commandBuffer, 0, 1, buffersPtr, offsetsPtr);
    }
  }

  private void Draw(VkCommandBuffer commandBuffer, uint stride) {
    vkCmdDraw(commandBuffer, stride, 1, 0, 0);
  }

  private void CreateVertexBuffer(TexturedVertex[] vertices) {
    _vertexCount = (ulong)vertices.Length;

    ulong bufferSize = ((ulong)Unsafe.SizeOf<TexturedVertex>()) * _vertexCount;
    ulong vertexSize = (ulong)Unsafe.SizeOf<TexturedVertex>();

    var stagingBuffer = new DwarfBuffer(
      _allocator,
      _device,
      vertexSize,
      _vertexCount,
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent
    );

    stagingBuffer.Map(bufferSize);
    unsafe {
      fixed (TexturedVertex* verticesPtr = vertices) {
        stagingBuffer.WriteToBuffer((nint)verticesPtr, bufferSize);
      }
    }
    // stagingBuffer.WriteToBuffer(MemoryUtils.ToIntPtr(vertices), bufferSize);

    _vertexBuffer = new DwarfBuffer(
      _allocator,
      _device,
      vertexSize,
      _vertexCount,
      BufferUsage.VertexBuffer | BufferUsage.TransferDst,
      MemoryProperty.DeviceLocal
    );

    // _device.CopyBuffer(stagingBuffer.GetBuffer(), _vertexBuffer.GetBuffer(), bufferSize);
    stagingBuffer.Dispose();
  }

  private void CreateBuffers() {
    _descriptorPool = new VulkanDescriptorPool.Builder(_device)
      .SetMaxSets(1000)
      .AddPoolSize(DescriptorType.UniformBufferDynamic, 1000)
      .SetPoolFlags(DescriptorPoolCreateFlags.UpdateAfterBind)
      .Build();

    _texturePool = new VulkanDescriptorPool.Builder(_device)
      .SetMaxSets(6)
      .AddPoolSize(DescriptorType.CombinedImageSampler, 1)
      .SetPoolFlags(DescriptorPoolCreateFlags.UpdateAfterBind)
      .Build();

    _skyboxBuffer = new DwarfBuffer(
      _allocator,
      _device,
      (ulong)Unsafe.SizeOf<SkyboxBufferObject>(),
      1,
      BufferUsage.UniformBuffer,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
      _device.Properties.limits.minUniformBufferOffsetAlignment
    );
  }

  private unsafe void CreatePipelineLayout(VkDescriptorSetLayout[] layouts) {
    var pipelineInfo = new VkPipelineLayoutCreateInfo() {
      setLayoutCount = (uint)layouts.Length
    };
    fixed (VkDescriptorSetLayout* layoutsPtr = layouts) {
      pipelineInfo.pSetLayouts = layoutsPtr;
    }
    VkPushConstantRange pushConstantRange = new() {
      stageFlags = VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment,
      offset = 0,
      size = (uint)Unsafe.SizeOf<SkyboxBufferObject>()
    };
    pipelineInfo.pushConstantRangeCount = 1;
    pipelineInfo.pPushConstantRanges = &pushConstantRange;
    vkCreatePipelineLayout(_device.LogicalDevice, &pipelineInfo, null, out _pipelineLayout).CheckResult();
  }

  private unsafe void CreatePipeline(
    VkRenderPass renderPass,
    string vertexName,
    string fragmentName,
    VkPipelineProvider pipelineProvider
  ) {
    _pipeline?.Dispose();
    _pipelineConfigInfo ??= new SkyboxPipeline();
    var pipelineConfig = _pipelineConfigInfo.GetConfigInfo();
    pipelineConfig.RenderPass = renderPass;
    pipelineConfig.PipelineLayout = _pipelineLayout;
    var colorFormat = _renderer.Swapchain.ColorFormat;
    var depthFormat = _renderer.DepthFormat;
    _pipeline = new VulkanPipeline(
      _device,
      vertexName,
      fragmentName,
      pipelineConfig,
      pipelineProvider,
      depthFormat.AsVkFormat(),
      colorFormat.AsVkFormat()
    );
  }

  protected unsafe void Dispose(bool disposing) {
    if (disposing) {
      _device.WaitQueue();
      _device.WaitDevice();
      _textureSetLayout?.Dispose();
      _descriptorPool?.Dispose();
      _texturePool?.Dispose();
      _pipeline?.Dispose();
      _vertexBuffer?.Dispose();
      _skyboxBuffer?.Dispose();
      vkDestroyPipelineLayout(_device.LogicalDevice, _pipelineLayout);
      _cubemapTexture.Dispose();
    }
  }

  public void Dispose() {
    Dispose(true);
    GC.SuppressFinalize(this);
  }
}
