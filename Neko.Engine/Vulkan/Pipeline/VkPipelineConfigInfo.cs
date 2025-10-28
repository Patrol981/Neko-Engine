using Neko.AbstractionLayer;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Neko.Vulkan;

public class VkPipelineConfigInfo : IPipelineConfigInfo {
  public VkPipelineViewportStateCreateInfo ViewportInfo;
  public VkPipelineInputAssemblyStateCreateInfo InputAssemblyInfo;
  public VkPipelineRasterizationStateCreateInfo RasterizationInfo;
  public VkPipelineMultisampleStateCreateInfo MultisampleInfo;
  public VkPipelineColorBlendAttachmentState ColorBlendAttachment;
  public VkPipelineColorBlendStateCreateInfo ColorBlendInfo;
  public VkPipelineDepthStencilStateCreateInfo DepthStencilInfo;
  public VkPipelineRenderingCreateInfo RenderingCreateInfo;

  public VkDynamicState[] DynamicStatesEnables = new VkDynamicState[0];
  public VkPipelineDynamicStateCreateInfo DynamicStateInfo;

  public ulong PipelineLayout { get; set; }
  public ulong RenderPass { get; set; } = VkRenderPass.Null;
  public uint Subpass;

  /// <summary>
  /// <c>PielineConfigInfo</c>.<c>GetConfigInfo()</c> returns default config info.
  /// This method is overridable, so there is no need to write all that stuff all over again if want to
  /// make small changes to the pipeline
  /// </summary>
  public virtual unsafe IPipelineConfigInfo GetConfigInfo() {
    var configInfo = this;

    // configInfo.InputAssemblyInfo.sType = VkStructureType.PipelineInputAssemblyStateCreateInfo;
    configInfo.InputAssemblyInfo = new() {
      topology = VkPrimitiveTopology.TriangleList,
      primitiveRestartEnable = false
    };

    // configInfo.ViewportInfo.sType = VkStructureType.PipelineViewportStateCreateInfo;
    configInfo.ViewportInfo = new() {
      viewportCount = 1,
      pViewports = null,
      scissorCount = 1,
      pScissors = null
    };

    // configInfo.RasterizationInfo.sType = VkStructureType.PipelineRasterizationStateCreateInfo;
    configInfo.RasterizationInfo = new() {
      depthClampEnable = true,
      rasterizerDiscardEnable = false,
      polygonMode = VkPolygonMode.Fill,
      lineWidth = 1.0f,
      cullMode = VkCullModeFlags.Back,
      frontFace = VkFrontFace.Clockwise,
      depthBiasEnable = false,
      depthBiasConstantFactor = 0.0f,  // Optional
      depthBiasClamp = 0.0f,           // Optional
      depthBiasSlopeFactor = 0.0f     // Optional
    };

    // configInfo.MultisampleInfo.sType = VkStructureType.PipelineMultisampleStateCreateInfo;
    configInfo.MultisampleInfo = new() {
      sampleShadingEnable = true,
      rasterizationSamples = VkSampleCountFlags.Count1,
      minSampleShading = 1.0f,           // Optional
      pSampleMask = null,             // Optional
      alphaToCoverageEnable = true,  // Optional
      alphaToOneEnable = true       // Optional
    };

    configInfo.ColorBlendAttachment.colorWriteMask = VkColorComponentFlags.R | VkColorComponentFlags.G | VkColorComponentFlags.B | VkColorComponentFlags.A;
    configInfo.ColorBlendAttachment.blendEnable = true;
    configInfo.ColorBlendAttachment.srcColorBlendFactor = VkBlendFactor.SrcAlpha;   // Optional
    configInfo.ColorBlendAttachment.dstColorBlendFactor = VkBlendFactor.OneMinusSrcAlpha;  // Optional
    configInfo.ColorBlendAttachment.colorBlendOp = VkBlendOp.Add;              // Optional
    configInfo.ColorBlendAttachment.srcAlphaBlendFactor = VkBlendFactor.One;   // Optional
    configInfo.ColorBlendAttachment.dstAlphaBlendFactor = VkBlendFactor.Zero;  // Optional
    configInfo.ColorBlendAttachment.alphaBlendOp = VkBlendOp.Add;              // Optional

    // configInfo.ColorBlendInfo.sType = VkStructureType.PipelineColorBlendStateCreateInfo;
    configInfo.ColorBlendInfo = new() {
      logicOpEnable = false,
      logicOp = VkLogicOp.Copy,  // Optional
      attachmentCount = 1,
    };
    fixed (VkPipelineColorBlendAttachmentState* colorBlendAttachment = &configInfo.ColorBlendAttachment) {
      configInfo.ColorBlendInfo.pAttachments = colorBlendAttachment;
    }
    // configInfo.ColorBlendInfo.pAttachments = &configInfo.ColorBlendAttachment;
    configInfo.ColorBlendInfo.blendConstants[0] = 0.0f;  // Optional
    configInfo.ColorBlendInfo.blendConstants[1] = 0.0f;  // Optional
    configInfo.ColorBlendInfo.blendConstants[2] = 0.0f;  // Optional
    configInfo.ColorBlendInfo.blendConstants[3] = 0.0f;  // Optional

    // configInfo.DepthStencilInfo.sType = VkStructureType.PipelineDepthStencilStateCreateInfo;
    configInfo.DepthStencilInfo = new() {
      depthTestEnable = true,
      depthWriteEnable = true,
      depthCompareOp = VkCompareOp.LessOrEqual,
      depthBoundsTestEnable = false,
      minDepthBounds = 0.0f,  // Optional
      maxDepthBounds = 1.0f,  // Optional
      stencilTestEnable = true,
      front = new(),  // Optional
      back = new()   // Optional
    };

    configInfo.PipelineLayout = VkPipelineLayout.Null;
    configInfo.RenderPass = VkRenderPass.Null;
    configInfo.Subpass = 0;

    configInfo.DynamicStatesEnables = [VkDynamicState.Viewport, VkDynamicState.Scissor];

    fixed (VkDynamicState* pStates = configInfo.DynamicStatesEnables) {
      // configInfo.DynamicStateInfo.sType = VkStructureType.PipelineDynamicStateCreateInfo;
      configInfo.DynamicStateInfo = new() {
        pDynamicStates = pStates,
        dynamicStateCount = (uint)configInfo.DynamicStatesEnables.Length,
        flags = VkPipelineDynamicStateCreateFlags.None
      };
    }

    return this;
  }
}