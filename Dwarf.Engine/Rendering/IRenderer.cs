using Dwarf.AbstractionLayer;
using Dwarf.Math;
using Dwarf.Vulkan;
using Vortice.Vulkan;

namespace Dwarf.Rendering;

public interface IRenderer : IDisposable {
  nint BeginFrame(CommandBufferLevel level = CommandBufferLevel.Primary);
  void EndFrame();
  void BeginRendering(nint commandBuffer);
  void EndRendering(nint commandBuffer);
  void RecreateSwapchain();
  void CreateCommandBuffers(ulong commandPool, CommandBufferLevel level = CommandBufferLevel.Primary);

  nint CurrentCommandBuffer { get; }
  int FrameIndex { get; }
  int ImageIndex { get; }
  float AspectRatio { get; }
  DwarfExtent2D Extent2D { get; }
  int MAX_FRAMES_IN_FLIGHT { get; }
  VulkanSwapchain Swapchain { get; }
  VulkanDynamicSwapchain DynamicSwapchain { get; }
  DwarfFormat DepthFormat { get; }
  CommandList CommandList { get; }

  ulong GetSwapchainRenderPass();
  ulong GetPostProcessingPass();

  void UpdateDescriptors();
  VkDescriptorSet PostProcessDecriptor { get; }
  VkDescriptorSet PreviousPostProcessDescriptor { get; }
}