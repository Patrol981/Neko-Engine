using Vortice.Vulkan;

namespace Dwarf.AbstractionLayer;

public enum DwarfColorSpace {
  SrgbNonLinear = 0,
}

public static class DwarfColorSpaceConverter {
  public static VkColorSpaceKHR AsVkColorSpace(this DwarfColorSpace colorSpace) => colorSpace switch {
    DwarfColorSpace.SrgbNonLinear => VkColorSpaceKHR.SrgbNonLinear,

    _ => throw new ArgumentException("Not supported")
  };

  public static DwarfColorSpace AsDwarfColorSpace(this VkColorSpaceKHR colorSpace) => colorSpace switch {
    VkColorSpaceKHR.SrgbNonLinear => DwarfColorSpace.SrgbNonLinear,

    _ => throw new ArgumentException("Not supported")
  };
}