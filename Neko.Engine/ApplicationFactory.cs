using Neko.AbstractionLayer;
using Neko.Rendering;
using Neko.Vulkan;

namespace Neko;

public enum ApplicationType {
  Default,
  Headless
}

internal static class ApplicationFactory {
  internal static void CreateDevice(in Application app) {
    switch (app.CurrentAPI) {
      case RenderAPI.Vulkan:
        VkCreateDevice(app);
        break;
      case RenderAPI.Metal:
        MCreateDevice(app);
        break;
      default:
        throw new NotImplementedException("Factory tried to create device that is not supported");
    }
  }

  private static void VkCreateDevice(in Application app) {
    VulkanDevice.s_EnableValidationLayers = app.Debug;
    app.Device = new VulkanDevice(app.Window);
  }

  private static void MCreateDevice(in Application app) {

  }

  internal static IStorageCollection CreateStorageCollection(nint allocator, IDevice device) {
    return device.RenderAPI switch {
      RenderAPI.Vulkan => VkCreateStorageCollection(allocator, device),
      RenderAPI.Metal => MCreateStorageCollection(allocator, device),
      _ => throw new NotImplementedException(),
    };
  }

  private static VkStorageCollection VkCreateStorageCollection(nint allocator, IDevice device) {
    return new VkStorageCollection(allocator, (VulkanDevice)device);
  }

  private static IStorageCollection MCreateStorageCollection(nint allocator, IDevice device) {
    throw new NotImplementedException();
  }
}