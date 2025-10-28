using Vortice.Vulkan;

namespace Neko.Vulkan;

public class VkLinePipeline : VkPipelineConfigInfo {
  public override VkPipelineConfigInfo GetConfigInfo() {
    var configInfo = base.GetConfigInfo() as VkPipelineConfigInfo;
    configInfo!.InputAssemblyInfo.topology = VkPrimitiveTopology.LineList;
    return configInfo;
  }
}
