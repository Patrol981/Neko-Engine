using Dwarf.Vulkan;
using Vortice.Vulkan;

namespace Dwarf.Rendering.Particles;

public class ParticlePipelineConfigInfo : VkPipelineConfigInfo {
  public override VkPipelineConfigInfo GetConfigInfo() {
    var configInfo = base.GetConfigInfo() as VkPipelineConfigInfo;

    configInfo!.DepthStencilInfo.depthWriteEnable = true;
    configInfo.DepthStencilInfo.depthCompareOp = VkCompareOp.GreaterOrEqual;

    configInfo.RasterizationInfo.cullMode = VkCullModeFlags.Back;

    configInfo.ColorBlendInfo.logicOp = VkLogicOp.Clear;

    configInfo.Subpass = 0;

    return configInfo;
  }
}