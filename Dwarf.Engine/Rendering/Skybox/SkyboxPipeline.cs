namespace Dwarf.Vulkan;

public class SkyboxPipeline : VkPipelineConfigInfo {
  public override VkPipelineConfigInfo GetConfigInfo() {
    var configInfo = base.GetConfigInfo();
    configInfo.RasterizationInfo.cullMode = Vortice.Vulkan.VkCullModeFlags.Back;
    configInfo.DepthStencilInfo.depthWriteEnable = false;
    configInfo.DepthStencilInfo.depthTestEnable = false;
    return configInfo;
  }
}
