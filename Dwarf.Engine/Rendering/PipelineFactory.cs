using Dwarf.AbstractionLayer;
using Dwarf.Vulkan;
using Vortice.Vulkan;

namespace Dwarf.Rendering;

public static class PipelineFactory {
  public static IPipeline CreatePipeline(
    Application app,
    string vertexName,
    string fragmentName,
    ref IPipelineConfigInfo pipelineConfigInfo,
    ref IPipelineProvider pipelineProvider,
    ulong pipelineLayout
  ) {
    switch (app.CurrentAPI) {
      case RenderAPI.Vulkan:
        return CreateVkPipeline(
          app,
          vertexName,
          fragmentName,
          ref pipelineConfigInfo,
          ref pipelineProvider,
          pipelineLayout
        );
      case RenderAPI.Metal:
        throw new NotImplementedException();
      default:
        throw new NotImplementedException();
    }
  }

  public static IPipelineConfigInfo GetOrCreatePipelineConfigInfo(Application app, IPipelineConfigInfo pipelineConfigInfo) {
    if (pipelineConfigInfo != null) return pipelineConfigInfo;

    switch (app.CurrentAPI) {
      case RenderAPI.Vulkan:
        return new VkPipelineConfigInfo();
      case RenderAPI.Metal:
        throw new NotImplementedException();
      default:
        throw new NotImplementedException();
    }
  }

  private static VulkanPipeline CreateVkPipeline(
    Application app,
    string vertexName,
    string fragmentName,
    ref IPipelineConfigInfo pipelineConfigInfo,
    ref IPipelineProvider pipelineProvider,
    ulong pipelineLayout
  ) {
    var pipelineConfig = pipelineConfigInfo;
    // var info = pipelineConfig.GetConfigInfo();
    var colorFormat = app.Renderer.DynamicSwapchain.ColorFormat;
    var depthFormat = app.Renderer.DepthFormat;

    // pipelineConfig.RenderPass = VkRenderPass.Null;
    pipelineConfig.PipelineLayout = pipelineLayout;

    return new VulkanPipeline(
      app.Device,
      vertexName,
      fragmentName,
      (VkPipelineConfigInfo)pipelineConfigInfo,
      (VkPipelineProvider)pipelineProvider,
      DwarfFormatConverter.AsVkFormat(depthFormat),
      colorFormat
    );
  }
}