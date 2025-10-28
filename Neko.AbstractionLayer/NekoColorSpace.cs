using Vortice.Vulkan;

namespace Neko.AbstractionLayer;

public enum NekoColorSpace {
  SrgbNonLinear = 0,
}

public static class NekoColorSpaceConverter {
  public static VkColorSpaceKHR AsVkColorSpace(this NekoColorSpace colorSpace) => colorSpace switch {
    NekoColorSpace.SrgbNonLinear => VkColorSpaceKHR.SrgbNonLinear,

    _ => throw new ArgumentException("Not supported")
  };

  public static NekoColorSpace AsNekoColorSpace(this VkColorSpaceKHR colorSpace) => colorSpace switch {
    VkColorSpaceKHR.SrgbNonLinear => NekoColorSpace.SrgbNonLinear,

    _ => throw new ArgumentException("Not supported")
  };
}