using System.Runtime.InteropServices;
using Dwarf.AbstractionLayer;
using Dwarf.Extensions.Logging;
using Dwarf.Math;
using Dwarf.Pathfinding;
using Dwarf.Rendering;
using Dwarf.Utils;
using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Neko.Vulkan;

public class VulkanDynamicSwapchain : ISwapchain {
  private readonly VulkanDevice _device;
  private VkSwapchainKHR _handle = VkSwapchainKHR.Null;


  public ulong[] Images { get; private set; } = [];
  public ulong[] ImageViews { get; private set; } = [];
  private VkFormat _colorFormat;
  public DwarfFormat ColorFormat => _colorFormat.AsDwarfFormat();
  private VkColorSpaceKHR _colorSpace = VkColorSpaceKHR.SrgbNonLinear;
  public DwarfColorSpace ColorSpace => _colorSpace.AsDwarfColorSpace();
  private VkExtent2D _extent2D;
  public DwarfExtent2D Extent2D => _extent2D.FromVkExtent2D();
  public uint QueueNodeIndex { get; private set; } = UInt32.MaxValue;

  public int CurrentFrame { get; set; }
  public int PreviousFrame { get; set; } = -1;

  private ulong _presentId = 0;

  public VulkanDynamicSwapchain(VulkanDevice device, VkExtent2D extent2D, bool vsync) {
    _device = device;
    _extent2D = extent2D;

    InitSurface();
    Init(vsync);
  }

  private unsafe void InitSurface() {
    // Get available queue family properties
    uint queueCount;
    _device.InstanceApi.vkGetPhysicalDeviceQueueFamilyProperties(_device.PhysicalDevice, &queueCount, null);

    VkQueueFamilyProperties* queueFamilyProperties = stackalloc VkQueueFamilyProperties[(int)queueCount];
    _device.InstanceApi.vkGetPhysicalDeviceQueueFamilyProperties(_device.PhysicalDevice, &queueCount, queueFamilyProperties);

    List<VkBool32> supportsPresent = new List<VkBool32>();
    for (uint i = 0; i < queueCount; i++) {
      _device.InstanceApi.vkGetPhysicalDeviceSurfaceSupportKHR(_device.PhysicalDevice, i, _device.Surface, out var supported);
      supportsPresent.Add(supported);
    }

    // Search for a graphics and a present queue in the array of queue
    // families, try to find one that supports both
    uint graphicsQueueNodeIndex = UInt32.MaxValue;
    uint presentQueueNodeIndex = UInt32.MaxValue;

    for (uint i = 0; i < queueCount; i++) {
      if ((queueFamilyProperties[i].queueFlags & VK_QUEUE_GRAPHICS_BIT) != 0) {
        if (graphicsQueueNodeIndex == UInt32.MaxValue) {
          graphicsQueueNodeIndex = i;
        }

        if (supportsPresent[(int)i] == VkBool32.True) {
          graphicsQueueNodeIndex = i;
          presentQueueNodeIndex = i;
          break;
        }
      }
    }
    if (presentQueueNodeIndex == UInt32.MaxValue) {
      // If there's no queue that supports both present and graphics
      // try to find a separate present queue
      for (uint i = 0; i < queueCount; ++i) {
        if (supportsPresent[(int)i] == VkBool32.True) {
          presentQueueNodeIndex = i;
          break;
        }
      }
    }

    // Exit if either a graphics or a presenting queue hasn't been found
    if (graphicsQueueNodeIndex == UInt32.MaxValue || presentQueueNodeIndex == UInt32.MaxValue) {
      throw new Exception("Could not find a graphics and/or presenting queue!");
    }

    if (graphicsQueueNodeIndex != presentQueueNodeIndex) {
      throw new Exception("Separate graphics and presenting queues are not supported yet!");
    }

    QueueNodeIndex = graphicsQueueNodeIndex;

    var swapChainSupport = VkUtils.QuerySwapChainSupport(_device, _device.PhysicalDevice, _device.Surface);
    var selectedFormat = ChooseSwapSurfaceFormat(swapChainSupport.Formats);

    _colorFormat = selectedFormat.format;
    _colorSpace = selectedFormat.colorSpace;
  }

  private static VkSurfaceFormatKHR ChooseSwapSurfaceFormat(ReadOnlySpan<VkSurfaceFormatKHR> availableFormats) {
    // If the surface format list only includes one entry with VK_FORMAT_UNDEFINED,
    // there is no preferred format, so we assume VK_FORMAT_B8G8R8A8_UNORM
    if ((availableFormats.Length == 1) && (availableFormats[0].format == VkFormat.Undefined)) {
      return new VkSurfaceFormatKHR(VkFormat.R8G8B8A8Unorm, availableFormats[0].colorSpace);
    }

    // iterate over the list of available surface format and
    // check for the presence of VK_FORMAT_B8G8R8A8_UNORM
    foreach (VkSurfaceFormatKHR availableFormat in availableFormats) {
      // R8G8B8A8Unorm
      // R8G8B8A8Srgb
      if (availableFormat.format == VkFormat.R8G8B8A8Unorm) {
        return availableFormat;
      } else if (availableFormat.format == VkFormat.B8G8R8A8Unorm) {
        return availableFormat;
      }
    }

    return availableFormats[0];
  }

  private unsafe void Init(bool vsync) {
    // VkSurfaceCapabilities2KHR surfCaps;
    // vkGetPhysicalDeviceSurfaceCapabilities2KHR(_device.PhysicalDevice, _device.Surface, &surfCaps).CheckResult();

    var support = VkUtils.QuerySwapChainSupport2(_device, _device.PhysicalDevice, _device.Surface);
    var surfCaps = support.Capabilities.surfaceCapabilities;

    // if (surfCaps.currentExtent.width < 1) {
    //   // Surface doesn't specify the size, so use our provided width and height.
    //   swapchainExtent.width = width;
    //   swapchainExtent.height = height;
    // } else {
    //   // Surface defines the extent, so use that.
    //   swapchainExtent = surfCaps.currentExtent;
    //   width = surfCaps.currentExtent.width;
    //   height = surfCaps.currentExtent.height;
    // }
    // Extent2D = swapchainExtent;

    // uint presentModeCount;
    // vkGetPhysicalDeviceSurfacePresentModesKHR(_device.PhysicalDevice, _device.Surface, &presentModeCount, null).CheckResult();

    // VkPresentModeKHR* presentModes = stackalloc VkPresentModeKHR[(int)presentModeCount];
    // vkGetPhysicalDeviceSurfacePresentModesKHR(_device.PhysicalDevice, _device.Surface, &presentModeCount, presentModes).CheckResult();

    var vkPresentModes = vkGetPhysicalDeviceSurfacePresentModesKHR(_device.PhysicalDevice, _device.Surface);
    VkPresentModeKHR swapchainPresentMode = VK_PRESENT_MODE_FIFO_KHR;

    if (!vsync) {
      for (int i = 0; i < vkPresentModes.Length; i++) {
        if (vkPresentModes[i] == VK_PRESENT_MODE_MAILBOX_KHR) {
          swapchainPresentMode = VK_PRESENT_MODE_MAILBOX_KHR;
          break;
        }
        if (vkPresentModes[i] == VK_PRESENT_MODE_IMMEDIATE_KHR) {
          swapchainPresentMode = VK_PRESENT_MODE_IMMEDIATE_KHR;
          break;
        }
        swapchainPresentMode = vkPresentModes[i];
      }

      Logger.Info($"[SWAPCHAIN] Present Mode set to {swapchainPresentMode}");
    }

    uint desiredNumberOfSwapchainImages = surfCaps.minImageCount + 1;

    if ((surfCaps.maxImageCount > 0) && (desiredNumberOfSwapchainImages > surfCaps.maxImageCount)) {
      desiredNumberOfSwapchainImages = surfCaps.maxImageCount;
    }

    // Find the transformation of the surface
    VkSurfaceTransformFlagsKHR preTransform;
    if ((surfCaps.supportedTransforms & VkSurfaceTransformFlagsKHR.Identity) != 0) {
      // We prefer a non-rotated transform
      preTransform = VK_SURFACE_TRANSFORM_IDENTITY_BIT_KHR;
    } else {
      preTransform = surfCaps.currentTransform;
    }

    // Find a supported composite alpha format (not all devices support alpha opaque)
    VkCompositeAlphaFlagsKHR compositeAlpha = VK_COMPOSITE_ALPHA_OPAQUE_BIT_KHR;
    // VkCompositeAlphaFlagsKHR compositeAlpha = VK_COMPOSITE_ALPHA_PRE_MULTIPLIED_BIT_KHR;
    // Simply select the first composite alpha format available
    VkCompositeAlphaFlagsKHR[] compositeAlphaFlags = [
      VK_COMPOSITE_ALPHA_OPAQUE_BIT_KHR,
      VK_COMPOSITE_ALPHA_PRE_MULTIPLIED_BIT_KHR,
      VK_COMPOSITE_ALPHA_POST_MULTIPLIED_BIT_KHR,
      VK_COMPOSITE_ALPHA_INHERIT_BIT_KHR,
    ];
    foreach (var flag in compositeAlphaFlags) {
      if ((surfCaps.supportedCompositeAlpha & flag) != 0) {
        compositeAlpha = flag;
        break;
      }
    }

    VkSwapchainPresentScalingCreateInfoKHR scalingCreateInfoEXT = new() {
      scalingBehavior = VkPresentScalingFlagsKHR.AspectRatioStretch,
      presentGravityX = VkPresentGravityFlagsKHR.Centered,
      presentGravityY = VkPresentGravityFlagsKHR.Centered,
    };

    VkSwapchainCreateInfoKHR swapchainCI = new() {
      surface = _device.Surface,
      minImageCount = desiredNumberOfSwapchainImages,
      imageFormat = ColorFormat.AsVkFormat(),
      imageColorSpace = _colorSpace,
      imageExtent = _extent2D,
      imageUsage = VkImageUsageFlags.ColorAttachment | VkImageUsageFlags.Sampled,
      preTransform = preTransform,
      imageArrayLayers = 1,
      imageSharingMode = VK_SHARING_MODE_EXCLUSIVE,
      queueFamilyIndexCount = 0,
      presentMode = swapchainPresentMode,
      clipped = true,
      // flags = VkSwapchainCreateFlagsKHR.PresentId2 | VkSwapchainCreateFlagsKHR.PresentWait2,
      compositeAlpha = compositeAlpha,
      oldSwapchain = _handle.Handle,
      pNext = &scalingCreateInfoEXT
    };

    _device.DeviceApi.vkCreateSwapchainKHR(_device.LogicalDevice, &swapchainCI, null, out _handle).CheckResult();

    uint imageCount = 0;
    _device.DeviceApi.vkGetSwapchainImagesKHR(_device.LogicalDevice, _handle, &imageCount, null).CheckResult();

    Images = new ulong[imageCount];
    fixed (ulong* imagesPtr = Images) {
      vkGetSwapchainImagesKHR(_device.LogicalDevice, _handle, &imageCount, (VkImage*)imagesPtr);
    }

    ImageViews = new ulong[imageCount];
    for (int i = 0; i < Images.Length; i++) {
      VkImageViewCreateInfo colorAttachmentView = new() {
        sType = VK_STRUCTURE_TYPE_IMAGE_VIEW_CREATE_INFO,
        pNext = null,
        format = ColorFormat.AsVkFormat(),
        components = new() {
          r = VK_COMPONENT_SWIZZLE_R,
          g = VK_COMPONENT_SWIZZLE_G,
          b = VK_COMPONENT_SWIZZLE_B,
          a = VK_COMPONENT_SWIZZLE_A
        }
      };
      colorAttachmentView.subresourceRange.aspectMask = VK_IMAGE_ASPECT_COLOR_BIT;
      colorAttachmentView.subresourceRange.baseMipLevel = 0;
      colorAttachmentView.subresourceRange.levelCount = 1;
      colorAttachmentView.subresourceRange.baseArrayLayer = 0;
      colorAttachmentView.subresourceRange.layerCount = 1;
      colorAttachmentView.viewType = VK_IMAGE_VIEW_TYPE_2D;
      colorAttachmentView.flags = 0;
      colorAttachmentView.image = Images[i];
      vkCreateImageView(_device.LogicalDevice, &colorAttachmentView, null, out var imageView).CheckResult();
      ImageViews[i] = imageView.Handle;
    }
  }

  public unsafe VkResult AcquireNextImage(VkSemaphore presentCompleteSemaphore, out uint imageIndex) {
    return _device.DeviceApi.vkAcquireNextImageKHR(
      _device.LogicalDevice,
      _handle,
      UInt64.MaxValue,
      presentCompleteSemaphore,
      VkFence.Null,
      out imageIndex
    );
  }

  public unsafe VkResult QueuePresent(VkQueue queue, uint imageIndex, VkSemaphore waitSemaphore, VkFence[] waitFences) {
    fixed (VkSwapchainKHR* pSwapchain = &_handle) {
      VkPresentInfoKHR presentInfo = new() {
        sType = VK_STRUCTURE_TYPE_PRESENT_INFO_KHR,
        pNext = null,
        swapchainCount = 1,
        pSwapchains = pSwapchain,
        pImageIndices = &imageIndex
      };
      if (waitSemaphore != VkSemaphore.Null) {
        presentInfo.pWaitSemaphores = &waitSemaphore;
        presentInfo.waitSemaphoreCount = 1;
      }
      var result = _device.DeviceApi.vkQueuePresentKHR(queue, &presentInfo);
      if (Application.Instance.Debug) {
        TracyWrapper.Profiler.HeartBeat();
      }

      // vkWaitForPresentKHR(
      //   _device.LogicalDevice,
      //   _handle,
      //   presentId,      // Wait for the specific present ID to complete
      //   ulong.MaxValue  // Wait indefinitely
      // ).CheckResult();

      fixed (VkFence* fencePtr = waitFences) {
        _device.DeviceApi.vkWaitForFences(_device.LogicalDevice, 1, fencePtr, true, ulong.MaxValue);
      }


      // vkDestroyFence(_device.LogicalDevice, waitFence, null);
      PreviousFrame = CurrentFrame;
      CurrentFrame = (CurrentFrame + 1) % Images.Length;
      return result;
    }
  }

  public float ExtentAspectRatio() {
    return _extent2D.width / (float)_extent2D.height;
  }

  public unsafe void Dispose() {
    Application.Mutex.WaitOne();
    _device.WaitDevice();

    if (_handle != VkSwapchainKHR.Null) {
      for (int i = 0; i < Images.Length; i++) {
        _device.DeviceApi.vkDestroyImageView(_device.LogicalDevice, ImageViews[i], null);
        // vkDestroyImage(_device.LogicalDevice, Images[i], null);
      }
      _device.DeviceApi.vkDestroySwapchainKHR(_device.LogicalDevice, _handle, null);
    }
    Application.Mutex.ReleaseMutex();
  }
}