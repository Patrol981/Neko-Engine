using Neko.AbstractionLayer;
using Neko.Math;
using Vortice.Vulkan;

namespace Neko.Rendering;

public interface ISwapchain : IDisposable {
  ulong[] Images { get; }
  ulong[] ImageViews { get; }
  NekoFormat ColorFormat { get; }
  VkFormat SurfaceFormat { get; }
  NekoColorSpace ColorSpace { get; }
  NekoExtent2D Extent2D { get; }
  uint QueueNodeIndex { get; }
  int CurrentFrame { get; set; }
  int PreviousFrame { get; set; }

  float ExtentAspectRatio();
}