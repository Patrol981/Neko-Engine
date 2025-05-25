using Dwarf.AbstractionLayer;
using Dwarf.Vulkan;
using Vortice.Vulkan;

namespace Dwarf.Rendering;

public static class PipelineFactory {
  public static IPipeline CreatePipeline(
    Application app,
    string vertexName,
    string fragmentName,
    object pipelineConfigInfo,
    object pipelineProvider,
    object depthFormat,
    object colorFormat
  ) {
    switch (app.CurrentAPI) {
      case RenderAPI.Vulkan:
        return CreateVkPipeline(
          app,
          vertexName,
          fragmentName,
          pipelineConfigInfo,
          pipelineProvider,
          depthFormat,
          colorFormat
        );
      case RenderAPI.Metal:
        throw new NotImplementedException();
      default:
        throw new NotImplementedException();
    }
  }

  private static IPipeline CreateVkPipeline(
    Application app,
    string vertexName,
    string fragmentName,
    object pipelineConfigInfo,
    object pipelineProvider,
    object depthFormat,
    object colorFormat
  ) {
    return new Vulkan.Pipeline(
      app.Device,
      vertexName,
      fragmentName,
      (VkPipelineConfigInfo)pipelineConfigInfo,
      (VkPipelineProvider)pipelineProvider,
      (VkFormat)depthFormat,
      (VkFormat)colorFormat
    );
  }
}