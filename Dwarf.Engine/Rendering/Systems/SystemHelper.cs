using Dwarf.AbstractionLayer;
using Dwarf.Vulkan;

using Vortice.Vulkan;

namespace Dwarf;

public class SystemHelper {
  // public static void CreatePipeline(ref VulkanDevice device, VkRenderPass renderPass, VkPipelineLayout layout, ref Pipeline pipeline, ref VkPipelineConfigInfo configInfo) {
  //   pipeline?.Dispose();
  //   if (configInfo != null) {
  //     configInfo = new VkPipelineConfigInfo();
  //   }
  //   var pipelineConfig = configInfo!.GetConfigInfo();
  //   pipelineConfig.RenderPass = renderPass;
  //   pipelineConfig.PipelineLayout = layout;
  //   // pipeline = new Pipeline(device, "gui_vertex", "gui_fragment", pipelineConfig);
  // }

  // public static IPipeline CreatePipeline(
  //   IDevice device,
  //   IRenderer renderer,
  //   string vertexName,
  //   string fragmentName,
  //   object pipelineConfigInfo,
  //   object pipelineProvider
  // ) {
  //   return device.RenderAPI switch {
  //     RenderAPI.Vulkan => new VulkanPipeline(
  //       device,
  //       vertexName,
  //       fragmentName,
  //       (VkPipelineConfigInfo)pipelineConfigInfo,
  //       (VkPipelineProvider)pipelineProvider,
  //       renderer.DepthFormat,
  //       renderer.DynamicSwapchain.ColorFormat
  //     ),
  //     _ => throw new NotImplementedException(),
  //   };
  // }
}
