using Dwarf.AbstractionLayer;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Vulkan;

public class VulkanPipeline : IPipeline {
  private readonly IDevice _device;

  private VkPipeline _graphicsPipeline;
  private VkShaderModule _vertexShaderModule;
  private VkShaderModule _fragmentShaderModule;
  private VkShaderModule _geometryShaderModule;
  private readonly VkPipelineProvider _pipelineProvider;

  private readonly Lock _pipelineLock = new();

  public VulkanPipeline(
    IDevice device,
    IPipelineConfigInfo configInfo,
    VkPipelineProvider pipelineProvider,
    VkFormat depthFormat,
    VkFormat colorFormat,
    string vertexName,
    string fragmentName
  ) {
    _device = device;
    _pipelineProvider = pipelineProvider;
    CreateGraphicsPipeline(
      (VkPipelineConfigInfo)configInfo,
      depthFormat,
      colorFormat,
      vertexName,
      fragmentName
    );
  }

  public VulkanPipeline(
    IDevice device,
    IPipelineConfigInfo configInfo,
    VkPipelineProvider pipelineProvider,
    VkFormat depthFormat,
    VkFormat colorFormat,
    string vertexName,
    string fragmentName,
    string? geometryName = null
  ) {
    _device = device;
    _pipelineProvider = pipelineProvider;
    CreateGraphicsPipeline(
      (VkPipelineConfigInfo)configInfo,
      depthFormat,
      colorFormat,
      vertexName,
      fragmentName,
      geometryName
    );
  }

  public void Bind(nint commandBuffer) {
    lock (_pipelineLock) {
      vkCmdBindPipeline(commandBuffer, VkPipelineBindPoint.Graphics, _graphicsPipeline);
    }
  }

  private unsafe void CreateGraphicsPipeline(
    VkPipelineConfigInfo configInfo,
    VkFormat depthFormat,
    VkFormat colorFormat,
    string vertexName,
    string fragmentName,
    string? geometryName = null
  ) {
    configInfo.RenderingCreateInfo = new() {
      colorAttachmentCount = 1,
      pColorAttachmentFormats = &colorFormat,
      depthAttachmentFormat = depthFormat,
    };

    var vertexPath = Path.Combine(AppContext.BaseDirectory, "CompiledShaders/Vulkan", $"{vertexName}.spv");
    var fragmentPath = Path.Combine(AppContext.BaseDirectory, "CompiledShaders/Vulkan", $"{fragmentName}.spv");
    var vertexCode = File.ReadAllBytes(vertexPath);
    var fragmentCode = File.ReadAllBytes(fragmentPath);

    CreateShaderModule(vertexCode, out _vertexShaderModule);
    CreateShaderModule(fragmentCode, out _fragmentShaderModule);

    if (geometryName != null) {
      var geometryPath = Path.Combine(AppContext.BaseDirectory, "CompiledShaders/Vulkan", $"{geometryName}.spv");
      var geometryCode = File.ReadAllBytes(geometryPath);
      CreateShaderModule(geometryCode, out _geometryShaderModule);
    }

    VkUtf8String entryPoint = "main"u8;
    VkPipelineShaderStageCreateInfo[] shaderStages =
    geometryName == null ?
    [
      new() {
        stage = VkShaderStageFlags.Vertex,
        module = _vertexShaderModule,
        pName = entryPoint,
        flags = 0,
        pNext = null
      },
      new() {
        stage = VkShaderStageFlags.Fragment,
        module = _fragmentShaderModule,
        pName = entryPoint,
        flags = 0,
        pNext = null
      }
    ] :
    [
      new() {
        stage = VkShaderStageFlags.Vertex,
        module = _vertexShaderModule,
        pName = entryPoint,
        flags = 0,
        pNext = null
      },
      new() {
        stage = VkShaderStageFlags.Fragment,
        module = _fragmentShaderModule,
        pName = entryPoint,
        flags = 0,
        pNext = null
      },
      new() {
        stage = VkShaderStageFlags.Geometry,
        module = _geometryShaderModule,
        pName = entryPoint,
        flags = 0,
        pNext = null
      }
    ];
    var bindingDescriptions = _pipelineProvider.GetBindingDescsFunc();
    var attributeDescriptions = _pipelineProvider.GetAttribDescsFunc();

    var vertexInputInfo = new VkPipelineVertexInputStateCreateInfo {
      vertexAttributeDescriptionCount = _pipelineProvider.GetAttribsLength(),
      vertexBindingDescriptionCount = _pipelineProvider.GetBindingsLength(),
      pVertexAttributeDescriptions = attributeDescriptions,
      pVertexBindingDescriptions = bindingDescriptions
    };

    var pipelineInfo = new VkGraphicsPipelineCreateInfo();
    pipelineInfo.stageCount = geometryName == null ? 2u : 3u;
    fixed (VkPipelineShaderStageCreateInfo* ptr = shaderStages) {
      pipelineInfo.pStages = ptr;
    }
    pipelineInfo.pVertexInputState = &vertexInputInfo;

    fixed (VkPipelineInputAssemblyStateCreateInfo* inputAssemblyInfo = &configInfo.InputAssemblyInfo)
    fixed (VkPipelineViewportStateCreateInfo* viewportInfo = &configInfo.ViewportInfo)
    fixed (VkPipelineRasterizationStateCreateInfo* rasterizationInfo = &configInfo.RasterizationInfo)
    fixed (VkPipelineMultisampleStateCreateInfo* multisampleInfo = &configInfo.MultisampleInfo)
    fixed (VkPipelineColorBlendStateCreateInfo* colorBlendInfo = &configInfo.ColorBlendInfo)
    fixed (VkPipelineDepthStencilStateCreateInfo* depthStencilInfo = &configInfo.DepthStencilInfo)
    fixed (VkPipelineRenderingCreateInfo* dynamicRenderInfo = &configInfo.RenderingCreateInfo)
    fixed (VkPipelineDynamicStateCreateInfo* dynamicStateInfo = &configInfo.DynamicStateInfo) {
      pipelineInfo.pInputAssemblyState = inputAssemblyInfo;
      pipelineInfo.pViewportState = viewportInfo;
      pipelineInfo.pRasterizationState = rasterizationInfo;
      pipelineInfo.pMultisampleState = multisampleInfo;
      pipelineInfo.pColorBlendState = colorBlendInfo;
      pipelineInfo.pDepthStencilState = depthStencilInfo;
      pipelineInfo.pDynamicState = dynamicStateInfo;
      pipelineInfo.pNext = dynamicRenderInfo;
    }

    pipelineInfo.layout = configInfo.PipelineLayout;

    pipelineInfo.basePipelineIndex = -1;
    pipelineInfo.basePipelineHandle = VkPipeline.Null;

    VkPipeline graphicsPipeline = VkPipeline.Null;

    var result = vkCreateGraphicsPipelines(
      _device.LogicalDevice,
      VkPipelineCache.Null,
      1,
      &pipelineInfo,
      null,
      &graphicsPipeline
    );
    if (result != VkResult.Success) throw new Exception("Failed to create graphics pipeline");

    _graphicsPipeline = graphicsPipeline;
  }

  private unsafe void CreateShaderModule(byte[] data, out VkShaderModule module) {
    vkCreateShaderModule(_device.LogicalDevice, data, null, out module).CheckResult();
  }

  public unsafe void Dispose() {
    vkDestroyShaderModule(_device.LogicalDevice, _vertexShaderModule, null);
    vkDestroyShaderModule(_device.LogicalDevice, _fragmentShaderModule, null);
    vkDestroyPipeline(_device.LogicalDevice, _graphicsPipeline);
  }
}