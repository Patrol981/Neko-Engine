using Dwarf.AbstractionLayer;
using Dwarf.Math;
using Dwarf.Vulkan;
using Vortice.Vulkan;

namespace Dwarf.Metal;

public class MRenderer : IRenderer {
  public MRenderer(Application app) {

  }

  public VkCommandBuffer CurrentCommandBuffer => throw new NotImplementedException();

  public int FrameIndex => throw new NotImplementedException();

  public int ImageIndex => throw new NotImplementedException();

  public float AspectRatio => throw new NotImplementedException();

  public DwarfExtent2D Extent2D => throw new NotImplementedException();

  public int MAX_FRAMES_IN_FLIGHT => throw new NotImplementedException();

  public VulkanSwapchain Swapchain => throw new NotImplementedException();

  public VulkanDynamicSwapchain DynamicSwapchain => throw new NotImplementedException();

  public VkFormat DepthFormat => throw new NotImplementedException();

  public CommandList CommandList => throw new NotImplementedException();

  public VkDescriptorSet PostProcessDecriptor => throw new NotImplementedException();

  public VkDescriptorSet PreviousPostProcessDescriptor => throw new NotImplementedException();

  public VkCommandBuffer BeginFrame(VkCommandBufferLevel level = VkCommandBufferLevel.Primary) {
    throw new NotImplementedException();
  }

  public void BeginRendering(VkCommandBuffer commandBuffer) {
    throw new NotImplementedException();
  }

  public void CreateCommandBuffers(VkCommandPool commandPool, VkCommandBufferLevel level = VkCommandBufferLevel.Primary) {
    throw new NotImplementedException();
  }

  public void Dispose() {
    throw new NotImplementedException();
  }

  public void EndFrame() {
    throw new NotImplementedException();
  }

  public void EndRendering(VkCommandBuffer commandBuffer) {
    throw new NotImplementedException();
  }

  public VkRenderPass GetPostProcessingPass() {
    throw new NotImplementedException();
  }

  public VkRenderPass GetSwapchainRenderPass() {
    throw new NotImplementedException();
  }

  public void RecreateSwapchain() {
    throw new NotImplementedException();
  }

  public void UpdateDescriptors() {
    throw new NotImplementedException();
  }
}