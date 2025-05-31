using Dwarf.Vulkan;

namespace Dwarf.Rendering.Renderer3D;

public class ModelPipelineConfig : VkPipelineConfigInfo {
  public override VkPipelineConfigInfo GetConfigInfo() {
    var configInfo = base.GetConfigInfo() as VkPipelineConfigInfo;
    configInfo!.Subpass = 0;
    return configInfo;
  }
}