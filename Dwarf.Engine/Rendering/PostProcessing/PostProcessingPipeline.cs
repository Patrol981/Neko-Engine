using Dwarf.Vulkan;
using Vortice.Vulkan;

namespace Dwarf.Rendering.PostProcessing;

public class PostProcessingPipeline : VkPipelineConfigInfo {
  public override VkPipelineConfigInfo GetConfigInfo() {
    var configInfo = base.GetConfigInfo();

    configInfo.ColorBlendInfo.attachmentCount = 1;
    configInfo.RasterizationInfo.cullMode = VkCullModeFlags.Back;
    configInfo.DepthStencilInfo.depthWriteEnable = false;
    configInfo.DepthStencilInfo.depthTestEnable = false;
    configInfo.Subpass = 0;
    return configInfo;
  }
}