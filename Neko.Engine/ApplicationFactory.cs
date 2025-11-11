using Neko.AbstractionLayer;
using Neko.Math;
using Neko.Rendering;
using Neko.Vulkan;

namespace Neko;

public enum ApplicationType {
  Default,
  Headless
}

public record class ApplicationInfo {
  public string? AppName;
  public SystemCreationFlags SystemCreationFlags;
  public SystemConfiguration? SystemConfiguration;
  public Vector2I? AppSize;
  public bool Fullscreen;
  public bool DebugMode;
  public bool VSync;
  public bool UseSkybox;
}

public static class ApplicationFactory {
  public static ApplicationInfo CreateApplicationBuilder() {
    return new ApplicationInfo();
  }

  public static ApplicationInfo WithName(this ApplicationInfo appInfo, string name) {
    appInfo.AppName = name;
    return appInfo;
  }

  public static ApplicationInfo WithCreationFlag(
    this ApplicationInfo appInfo,
    SystemCreationFlags systemCreationFlag
  ) {
    appInfo.SystemCreationFlags |= systemCreationFlag;
    return appInfo;
  }

  public static ApplicationInfo WithSystemConfiguration(
    this ApplicationInfo appInfo,
    SystemConfiguration systemConfiguration
  ) {
    appInfo.SystemConfiguration = systemConfiguration;
    return appInfo;
  }

  public static ApplicationInfo WithSize(this ApplicationInfo appInfo, Vector2I size) {
    appInfo.AppSize = size;
    return appInfo;
  }

  public static ApplicationInfo WithVSync(this ApplicationInfo appInfo, bool value = true) {
    appInfo.VSync = value;
    return appInfo;
  }

  public static ApplicationInfo WithFullscreen(this ApplicationInfo appInfo, bool value = true) {
    appInfo.Fullscreen = value;
    return appInfo;
  }

  public static ApplicationInfo WithDebugMode(this ApplicationInfo appInfo, bool value = true) {
    appInfo.DebugMode = value;
    return appInfo;
  }

  public static ApplicationInfo WithSkybox(this ApplicationInfo appInfo, bool value = true) {
    appInfo.UseSkybox = value;
    return appInfo;
  }

  public static Application Build(this ApplicationInfo appInfo) {
    var app = new Application(
      appName: appInfo.AppName ?? "Neko App",
      windowSize: appInfo.AppSize ?? new(1400, 900),
      systemCreationFlags: appInfo.SystemCreationFlags,
      vsync: appInfo.VSync,
      fullscreen: appInfo.Fullscreen,
      debugMode: appInfo.DebugMode,
      systemConfiguration: appInfo.SystemConfiguration
    ) {
      UseSkybox = appInfo.UseSkybox
    };

    return app;
  }

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