using Vortice.Vulkan;

namespace Dwarf.Vulkan;

public class VkLinePipeline : VkPipelineConfigInfo {
  public override VkPipelineConfigInfo GetConfigInfo() {
    var configInfo = base.GetConfigInfo();
    configInfo.InputAssemblyInfo.topology = VkPrimitiveTopology.LineList;
    return configInfo;
  }
}
