using Vortice.Vulkan;

namespace Neko.Rendering;

public struct ThreadInfo {
  public VkCommandPool CommandPool;
  public VkCommandBuffer[] CommandBuffer;
}
