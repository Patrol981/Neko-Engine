using Dwarf.Vulkan;

using Vortice.Vulkan;

namespace Dwarf.Rendering.UI;

public class ImGuiPipeline : VkPipelineConfigInfo {
  public override unsafe VkPipelineConfigInfo GetConfigInfo() {
    var configInfo = base.GetConfigInfo() as VkPipelineConfigInfo;
    var frontFace = VkFrontFace.CounterClockwise;

    configInfo!.InputAssemblyInfo = VkUtils.PipelineInputAssemblyStateCreateInfo(
      VkPrimitiveTopology.TriangleList,
      0,
      false
    );

    configInfo.RasterizationInfo = VkUtils.PipelineRasterizationStateCreateInfo(
      VkPolygonMode.Fill,
      VkCullModeFlags.None,
      frontFace
    );

    // VkPipelineColorBlendAttachmentState blendAttachmentState = new();
    configInfo.ColorBlendAttachment.blendEnable = true;
    configInfo.ColorBlendAttachment.colorWriteMask = VkColorComponentFlags.R | VkColorComponentFlags.G | VkColorComponentFlags.B | VkColorComponentFlags.A;
    configInfo.ColorBlendAttachment.srcColorBlendFactor = VkBlendFactor.SrcAlpha;
    configInfo.ColorBlendAttachment.dstColorBlendFactor = VkBlendFactor.OneMinusSrcAlpha;
    configInfo.ColorBlendAttachment.colorBlendOp = VkBlendOp.Add;
    configInfo.ColorBlendAttachment.srcAlphaBlendFactor = VkBlendFactor.One;
    configInfo.ColorBlendAttachment.dstAlphaBlendFactor = VkBlendFactor.OneMinusSrcAlpha;
    configInfo.ColorBlendAttachment.alphaBlendOp = VkBlendOp.Add;
    // configInfo.ColorBlendAttachment = blendAttachmentState;

    // configInfo.ColorBlendInfo = VkUtils.PipelineColorBlendStateCreateInfo(1, &configInfo.ColorBlendAttachment);
    configInfo.DepthStencilInfo = VkUtils.PipelineDepthStencilStateCreateInfo(false, false, VkCompareOp.LessOrEqual);
    configInfo.ViewportInfo = VkUtils.PipelineViewportStateCreateInfo(1, 1, 0);
    configInfo.MultisampleInfo = VkUtils.PipelineMultisampleStateCreateInfo(VkSampleCountFlags.Count1);

    configInfo.Subpass = 0;

    return configInfo;
  }
}
