using Vortice.Vulkan;

namespace Neko.Vulkan;

public class UIPipeline : VkPipelineConfigInfo {
  public override VkPipelineConfigInfo GetConfigInfo() {
    var configInfo = base.GetConfigInfo() as VkPipelineConfigInfo;
    configInfo!.DepthStencilInfo.front.compareOp = VkCompareOp.Never;
    configInfo.DepthStencilInfo.front.passOp = VkStencilOp.Keep;
    configInfo.DepthStencilInfo.front.failOp = VkStencilOp.Keep;
    configInfo.DepthStencilInfo.front.depthFailOp = VkStencilOp.Keep;

    configInfo.DepthStencilInfo.back.compareOp = VkCompareOp.Never;
    configInfo.DepthStencilInfo.back.passOp = VkStencilOp.Keep;
    configInfo.DepthStencilInfo.back.failOp = VkStencilOp.Keep;
    configInfo.DepthStencilInfo.back.depthFailOp = VkStencilOp.Keep;

    configInfo.DepthStencilInfo.depthTestEnable = false;
    configInfo.DepthStencilInfo.depthBoundsTestEnable = false;
    configInfo.DepthStencilInfo.depthWriteEnable = false;

    configInfo.RasterizationInfo.frontFace = VkFrontFace.Clockwise;
    configInfo.RasterizationInfo.cullMode = VkCullModeFlags.Back;

    return configInfo;
  }
}
