using Dwarf.AbstractionLayer;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Vulkan;

public struct PipelineConfigInfoStruct {
  public VkPipelineViewportStateCreateInfo ViewportInfo;
  public VkPipelineInputAssemblyStateCreateInfo InputAssemblyInfo;
  public VkPipelineRasterizationStateCreateInfo RasterizationInfo;
  public VkPipelineMultisampleStateCreateInfo MultisampleInfo;
  public VkPipelineColorBlendAttachmentState ColorBlendAttachment;
  public VkPipelineColorBlendStateCreateInfo ColorBlendInfo;
  public VkPipelineDepthStencilStateCreateInfo DepthStencilInfo;

  public VkDynamicState[] DynamicStatesEnables;
  public VkPipelineDynamicStateCreateInfo DynamicStateInfo;

  public VkPipelineLayout PipelineLayout;
  public VkRenderPass RenderPass;
  public uint Subpass;
}

public class Pipeline : IDisposable {
  private readonly IDevice _device;

  private VkPipeline _graphicsPipeline;
  private VkShaderModule _vertexShaderModule;
  private VkShaderModule _fragmentShaderModule;
  private readonly PipelineProvider _pipelineProvider;

  private readonly object _pipelineLock = new();

  public Pipeline(
    IDevice device,
    string vertexName,
    string fragmentName,
    PipelineConfigInfo configInfo,
    PipelineProvider pipelineProvider,
    VkFormat depthFormat,
    VkFormat colorFormat
  ) {
    _device = device;
    _pipelineProvider = pipelineProvider;
    CreateGraphicsPipeline(vertexName, fragmentName, configInfo, depthFormat, colorFormat);
  }

  public void Bind(VkCommandBuffer commandBuffer) {
    lock (_pipelineLock) {
      vkCmdBindPipeline(commandBuffer, VkPipelineBindPoint.Graphics, _graphicsPipeline);
    }
  }

  private unsafe void CreateGraphicsPipeline(
    string vertexName,
    string fragmentName,
    PipelineConfigInfo configInfo,
    VkFormat depthFormat,
    VkFormat colorFormat
  ) {
    configInfo.RenderingCreateInfo = new() {
      colorAttachmentCount = 1,
      pColorAttachmentFormats = &colorFormat,
      depthAttachmentFormat = depthFormat,
      // stencilAttachmentFormat = depthFormat
    };

    var vertexPath = Path.Combine(AppContext.BaseDirectory, "CompiledShaders/Vulkan", $"{vertexName}.spv");
    var fragmentPath = Path.Combine(AppContext.BaseDirectory, "CompiledShaders/Vulkan", $"{fragmentName}.spv");
    var vertexCode = File.ReadAllBytes(vertexPath);
    var fragmentCode = File.ReadAllBytes(fragmentPath);

    CreateShaderModule(vertexCode, out _vertexShaderModule);
    CreateShaderModule(fragmentCode, out _fragmentShaderModule);

    VkUtf8String entryPoint = "main"u8;
    VkPipelineShaderStageCreateInfo[] shaderStages = new VkPipelineShaderStageCreateInfo[2];

    //vertex
    // shaderStages[0].sType = VkStructureType.PipelineShaderStageCreateInfo;
    shaderStages[0] = new();
    shaderStages[0].stage = VkShaderStageFlags.Vertex;
    shaderStages[0].module = _vertexShaderModule;
    shaderStages[0].pName = entryPoint;
    shaderStages[0].flags = 0;
    shaderStages[0].pNext = null;

    //fragment
    // shaderStages[1].sType = VkStructureType.PipelineShaderStageCreateInfo;
    shaderStages[1] = new();
    shaderStages[1].stage = VkShaderStageFlags.Fragment;
    shaderStages[1].module = _fragmentShaderModule;
    shaderStages[1].pName = entryPoint;
    shaderStages[1].flags = 0;
    shaderStages[1].pNext = null;

    var bindingDescriptions = _pipelineProvider.GetBindingDescsFunc();
    var attributeDescriptions = _pipelineProvider.GetAttribDescsFunc();

    var vertexInputInfo = new VkPipelineVertexInputStateCreateInfo();
    // vertexInputInfo.sType = VkStructureType.PipelineVertexInputStateCreateInfo;
    vertexInputInfo.vertexAttributeDescriptionCount = _pipelineProvider.GetAttribsLength();
    vertexInputInfo.vertexBindingDescriptionCount = _pipelineProvider.GetBindingsLength();
    vertexInputInfo.pVertexAttributeDescriptions = attributeDescriptions;
    vertexInputInfo.pVertexBindingDescriptions = bindingDescriptions;

    var pipelineInfo = new VkGraphicsPipelineCreateInfo();
    // pipelineInfo.sType = VkStructureType.GraphicsPipelineCreateInfo;
    pipelineInfo.stageCount = 2;
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
    // pipelineInfo.pInputAssemblyState = &configInfo.InputAssemblyInfo;
    // pipelineInfo.pViewportState = &configInfo.ViewportInfo;
    // pipelineInfo.pRasterizationState = &configInfo.RasterizationInfo;
    // pipelineInfo.pMultisampleState = &configInfo.MultisampleInfo;
    // pipelineInfo.pColorBlendState = &configInfo.ColorBlendInfo;
    // pipelineInfo.pDepthStencilState = &configInfo.DepthStencilInfo;
    // pipelineInfo.pDynamicState = &configInfo.DynamicStateInfo;

    pipelineInfo.layout = configInfo.PipelineLayout;
    // pipelineInfo.renderPass = configInfo.RenderPass;
    // pipelineInfo.subpass = configInfo.Subpass;

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