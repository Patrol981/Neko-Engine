using Dwarf.AbstractionLayer;
using Dwarf.Math;

namespace Dwarf.Rendering;

public interface ISwapchain : IDisposable {
  ulong[] Images { get; }
  ulong[] ImageViews { get; }
  DwarfFormat ColorFormat { get; }
  DwarfColorSpace ColorSpace { get; }
  DwarfExtent2D Extent2D { get; }
  uint QueueNodeIndex { get; }
  int CurrentFrame { get; set; }
  int PreviousFrame { get; set; }

  float ExtentAspectRatio();
}