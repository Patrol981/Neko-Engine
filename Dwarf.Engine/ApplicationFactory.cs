using Dwarf.AbstractionLayer;
using Dwarf.Rendering;
using Dwarf.Vulkan;

namespace Dwarf;

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

  internal static void CreateAllocator(in Application app) {
    switch (app.CurrentAPI) {
      case RenderAPI.Vulkan:
        VkCreateAllocator(app);
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

  private static void VkCreateAllocator(in Application app) {
    ResourceInitializer.VkInitAllocator(app.Device, out var allocator);
    app.Allocator = allocator;
  }

  private static void MCreateAllocator(in Application app) {

  }
}