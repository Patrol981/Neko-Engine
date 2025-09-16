using System.Text;
using Dwarf.Extensions.Logging;
using Dwarf.Utils;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Vulkan;

public static unsafe class DeviceHelper {
  public static VkPhysicalDevice GetPhysicalDevice(VulkanDevice device, VkInstance instance, VkSurfaceKHR surface) {
    VkPhysicalDevice returnDevice = VkPhysicalDevice.Null;

    uint count = 0;
    device.InstanceApi.vkEnumeratePhysicalDevices(instance, out count).CheckResult();
    if (count == 0) {
      Logger.Error("Failed to find any Vulkan capable GPU");
    }
    Span<VkPhysicalDevice> physicalDevices = stackalloc VkPhysicalDevice[(int)count];
    device.InstanceApi.vkEnumeratePhysicalDevices(instance, physicalDevices);

    VkPhysicalDeviceProperties gpuInfo = new();

    Logger.Info("Available GPU'S:");

    for (int i = 0; i < count; i++) {
      VkPhysicalDevice physicalDevice = physicalDevices[i];
      if (IsDeviceSuitable(device, physicalDevice, surface) == false)
        continue;

      VkPhysicalDeviceProperties2 checkProperties = new();
      device.InstanceApi.vkGetPhysicalDeviceProperties2(physicalDevice, &checkProperties);
      var deviceName = ByteConverter.BytePointerToStringUTF8(checkProperties.properties.deviceName);
      Logger.Info(deviceName);
      bool discrete = checkProperties.properties.deviceType == VkPhysicalDeviceType.DiscreteGpu;
      if (discrete || returnDevice.IsNull) {
        gpuInfo = checkProperties.properties;
        returnDevice = physicalDevice;
        if (discrete) break;
      }
    }

    var gpuName = ByteConverter.BytePointerToStringUTF8(gpuInfo.deviceName);
    Logger.Info($"Successfully found a device: {gpuName}");

    return returnDevice;
  }
  public static bool IsSupported() {
    try {
      VkResult result = vkInitialize();
      if (result != VkResult.Success)
        return false;
      VkVersion version = vkEnumerateInstanceVersion();
      return version >= VkVersion.Version_1_4;
    } catch {
      return false;
    }
  }

  public static VkUtf8String[] EnumerateInstanceLayers() {
    if (!IsSupported()) {
      return [];
    }

    uint count = 0;
    VkResult result = vkEnumerateInstanceLayerProperties(&count, null);
    if (result != VkResult.Success) {
      return [];
    }

    if (count == 0) {
      return [];
    }

    VkLayerProperties[] properties = new VkLayerProperties[count];

    fixed (VkLayerProperties* ptr = properties) {
      vkEnumerateInstanceLayerProperties(&count, ptr).CheckResult();
    }

    VkUtf8String[] resultExt = new VkUtf8String[count];
    for (int i = 0; i < count; i++) {
      fixed (byte* pLayerName = properties[i].layerName) {
        resultExt[i] = new VkUtf8String(pLayerName);
      }
    }

    return resultExt;
  }

  public static void GetOptimalValidationLayers(HashSet<VkUtf8String> availableLayers, List<VkUtf8String> instanceLayers) {
    // The preferred validation layer is "VK_LAYER_KHRONOS_validation"
    List<VkUtf8String> validationLayers =
    [
       "VK_LAYER_KHRONOS_validation"u8
    ];

    if (ValidateLayers(validationLayers, availableLayers)) {
      instanceLayers.AddRange(validationLayers);
      return;
    }

    // Otherwise we fallback to using the LunarG meta layer
    validationLayers = [
       "VK_LAYER_LUNARG_standard_validation"u8
    ];

    if (ValidateLayers(validationLayers, availableLayers)) {
      instanceLayers.AddRange(validationLayers);
      return;
    }

    // Otherwise we attempt to enable the individual layers that compose the LunarG meta layer since it doesn't exist
    validationLayers = [
      "VK_LAYER_GOOGLE_threading"u8,
      "VK_LAYER_LUNARG_parameter_validation"u8,
      "VK_LAYER_LUNARG_object_tracker"u8,
      "VK_LAYER_LUNARG_core_validation"u8,
      "VK_LAYER_GOOGLE_unique_objects"u8,
    ];

    if (ValidateLayers(validationLayers, availableLayers)) {
      instanceLayers.AddRange(validationLayers);
      return;
    }

    // Otherwise as a last resort we fallback to attempting to enable the LunarG core layer
    validationLayers = [
      "VK_LAYER_LUNARG_core_validation"u8
    ];

    if (ValidateLayers(validationLayers, availableLayers)) {
      instanceLayers.AddRange(validationLayers);
      return;
    }
  }

  private static bool ValidateLayers(List<VkUtf8String> required, HashSet<VkUtf8String> availableLayers) {
    foreach (VkUtf8String layer in required) {
      bool found = false;
      foreach (VkUtf8String availableLayer in availableLayers) {
        if (availableLayer == layer) {
          found = true;
          break;
        }
      }

      if (!found) {
        //Log.Warn("Validation Layer '{}' not found", layer);
        return false;
      }
    }

    return true;
  }

  private static bool IsDeviceSuitable(VulkanDevice device, VkPhysicalDevice physicalDevice, VkSurfaceKHR surface) {
    var checkQueueFamilies = FindQueueFamilies2(device, physicalDevice, surface);
    if (checkQueueFamilies.graphicsFamily == VK_QUEUE_FAMILY_IGNORED)
      return false;

    if (checkQueueFamilies.presentFamily == VK_QUEUE_FAMILY_IGNORED)
      return false;

    SwapChainSupportDetails2 swapChainSupport = VkUtils.QuerySwapChainSupport2(device, physicalDevice, surface);
    return !swapChainSupport.Formats.IsEmpty && !swapChainSupport.PresentModes.IsEmpty;
  }

  public static VkUtf8String[] GetInstanceExtensions() {
    uint count = 0;
    VkResult result = vkEnumerateInstanceExtensionProperties(&count, null);
    if (result != VkResult.Success) {
      return [];
    }

    if (count == 0) {
      return [];
    }

    VkExtensionProperties[] props = new VkExtensionProperties[count];
    fixed (VkExtensionProperties* ptr = props) {
      vkEnumerateInstanceExtensionProperties(&count, ptr);
    }

    VkUtf8String[] extensions = new VkUtf8String[count];
    for (int i = 0; i < count; i++) {
      fixed (byte* pExtensionName = props[i].extensionName) {
        extensions[i] = new VkUtf8String(pExtensionName);
      }

    }

    return extensions;
  }

  public static bool IsExtenstionPresent(this IList<VkUtf8String> @string, VkUtf8String extensionName) {
    var sList = new List<string>();
    foreach (var s in @string) {
      sList.Add(Encoding.UTF8.GetString(s));
    }
    var target = Encoding.UTF8.GetString(extensionName);

    foreach (var item in @string) {
      // var strItem = Encoding.UTF8.GetString(item);
      if (item == extensionName) {
        return true;
      }
    }
    return false;
  }

  public static bool CheckDeviceExtensionSupport(VkUtf8ReadOnlyString extensionName, ReadOnlySpan<VkExtensionProperties> availableDeviceExtensions) {
    foreach (VkExtensionProperties property in availableDeviceExtensions) {
      if (extensionName == property.extensionName)
        return true;
    }

    return false;
  }

  public static List<VkUtf8String> CreateEnabledExtensionsList(
    VkPhysicalDevice physicalDevice,
    params VkUtf8String[] extensions
  ) {
    //var availableDeviceExtensions = vkEnumerateDeviceExtensionProperties(physicalDevice);
    // var extList = new List<VkUtf8String>();

    // foreach (var ext in extensions) {
    //   if (CheckDeviceExtensionSupport(ext, availableDeviceExtensions)) {
    //     extList.Add(ext);
    //   }
    // }

    // return extList;
    return [.. extensions];
  }

  // public unsafe static List<VkUtf8String> CreateEnabledExtensionsList(
  //   List<VkUtf8String> availableExtensions,
  //   VkPhysicalDevice physicalDevice,
  //   params VkUtf8String[] extensions
  // ) {
  //   // var enabledExtensions = new List<VkUtf8String>();
  //   // var instanceExtensions = GetInstanceExtensions();

  //   // foreach (var ext in extensions) {
  //   //   if (availableExtensions.IsExtenstionPresent(ext)) {
  //   //     enabledExtensions.Add(ext);
  //   //   }
  //   // }

  //   uint count = 0;
  //   vkEnumerateDeviceExtensionProperties(physicalDevice, null, &count, null).CheckResult();

  //   var extProperties = stackalloc VkExtensionProperties[(int)count];
  //   vkEnumerateDeviceExtensionProperties(physicalDevice, null, &count, extProperties).CheckResult();

  //   List<string> names = [];
  //   Span<byte> dest;
  //   for (int i = 0; i < count; i++) {
  //     var ext = extProperties[i];

  //     var str = ext.extensionName->ToString();
  //     names.Add(str);
  //   }

  //   return availableExtensions;
  // }

  public static (uint graphicsFamily, uint presentFamily) FindQueueFamilies(
      VulkanDevice device, VkSurfaceKHR surface) {

    device.InstanceApi.vkGetPhysicalDeviceQueueFamilyProperties(device.PhysicalDevice, out uint famCount);
    Span<VkQueueFamilyProperties> queueFamilies = stackalloc VkQueueFamilyProperties[(int)famCount];
    device.InstanceApi.vkGetPhysicalDeviceQueueFamilyProperties(device.PhysicalDevice, queueFamilies);

    uint graphicsFamily = VK_QUEUE_FAMILY_IGNORED;
    uint presentFamily = VK_QUEUE_FAMILY_IGNORED;
    uint i = 0;
    foreach (VkQueueFamilyProperties queueFamily in queueFamilies) {
      if ((queueFamily.queueFlags & VkQueueFlags.Graphics) != VkQueueFlags.None) {
        graphicsFamily = i;
      }

      device.InstanceApi.vkGetPhysicalDeviceSurfaceSupportKHR(device.PhysicalDevice, i, surface, out VkBool32 presentSupport);
      if (presentSupport) {
        presentFamily = i;
      }

      if (graphicsFamily != VK_QUEUE_FAMILY_IGNORED
          && presentFamily != VK_QUEUE_FAMILY_IGNORED) {
        break;
      }

      i++;
    }

    return (graphicsFamily, presentFamily);
  }

  public static (uint graphicsFamily, uint presentFamily) FindQueueFamilies2(
    VulkanDevice device,
    VkPhysicalDevice physicalDevice,
    VkSurfaceKHR surface
  ) {
    // var queueFamilies = vkGetPhysicalDeviceQueueFamilyProperties2(device);

    uint propCount = 0;
    device.InstanceApi.vkGetPhysicalDeviceQueueFamilyProperties2(physicalDevice, &propCount, null);
    var props = new VkQueueFamilyProperties2[propCount];
    for (int x = 0; x < props.Length; x++) {
      props[x].sType = VkStructureType.QueueFamilyProperties2;
      props[x].pNext = null;
    }
    fixed (VkQueueFamilyProperties2* propPtr = props) {
      device.InstanceApi.vkGetPhysicalDeviceQueueFamilyProperties2(physicalDevice, &propCount, propPtr);
    }

    uint graphicsFamily = VK_QUEUE_FAMILY_IGNORED;
    uint presentFamily = VK_QUEUE_FAMILY_IGNORED;
    uint i = 0;

    foreach (VkQueueFamilyProperties2 queueFamily in props) {
      if ((queueFamily.queueFamilyProperties.queueFlags & VkQueueFlags.Graphics) != VkQueueFlags.None) {
        graphicsFamily = i;
      }

      device.InstanceApi.vkGetPhysicalDeviceSurfaceSupportKHR(physicalDevice, i, surface, out VkBool32 presentSupport);
      if (presentSupport) {
        presentFamily = i;
      }

      if (graphicsFamily != VK_QUEUE_FAMILY_IGNORED
          && presentFamily != VK_QUEUE_FAMILY_IGNORED) {
        break;
      }

      i++;
    }

    return (graphicsFamily, presentFamily);
  }
}