using Neko.Vulkan;
using Vortice.Vulkan;

namespace Neko.Rendering.Renderer3D;

public class VertexDebugPipeline : VkPipelineConfigInfo {
  public override VkPipelineConfigInfo GetConfigInfo() {
    var configInfo = base.GetConfigInfo() as VkPipelineConfigInfo;
    configInfo!.RasterizationInfo.polygonMode = VkPolygonMode.Line;
    configInfo.RasterizationInfo.lineWidth = 1.0f;

    configInfo.Subpass = 0;
    return configInfo;
  }
}