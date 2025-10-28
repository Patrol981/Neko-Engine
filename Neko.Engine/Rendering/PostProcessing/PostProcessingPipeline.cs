using Neko.Vulkan;
using Vortice.Vulkan;

namespace Neko.Rendering.PostProcessing;

public class PostProcessingPipeline : VkPipelineConfigInfo {
  public override VkPipelineConfigInfo GetConfigInfo() {
    var configInfo = base.GetConfigInfo() as VkPipelineConfigInfo;

    configInfo!.ColorBlendInfo.attachmentCount = 1;
    configInfo.RasterizationInfo.cullMode = VkCullModeFlags.Back;
    configInfo.DepthStencilInfo.depthWriteEnable = false;
    configInfo.DepthStencilInfo.depthTestEnable = false;
    configInfo.Subpass = 0;
    return configInfo;
  }
}