using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

using Dwarf.AbstractionLayer;
using Dwarf.Extensions.Logging;
using Dwarf.Windowing;

using Vortice.Vulkan;

using static SDL3.SDL3;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Vulkan;

public class VulkanDevice : IDevice {
  public RenderAPI RenderAPI => RenderAPI.Vulkan;

  private readonly string[] VALIDATION_LAYERS = ["VK_LAYER_KHRONOS_validation"];
  public static bool s_EnableValidationLayers = true;
  private readonly IWindow _window;

  private VkDebugUtilsMessengerEXT _debugMessenger = VkDebugUtilsMessengerEXT.Null;

  private VkInstance _vkInstance = VkInstance.Null;
  private VkPhysicalDevice _physicalDevice = VkPhysicalDevice.Null;
  private VkDevice _logicalDevice = VkDevice.Null;

  private readonly VkCommandPool _commandPool = VkCommandPool.Null;
  private readonly Lock _commandPoolLock = new();

  private VkQueue _graphicsQueue = VkQueue.Null;
  // private VkQueue _presentQueue = VkQueue.Null;
  // private readonly VkQueue _transferQueue = VkQueue.Null;

  internal readonly Lock _queueLock = new();

  // private readonly VkFence _singleTimeFence = VkFence.Null;

  // private readonly VkSemaphore _semaphore = VkSemaphore.Null;
  // private readonly ulong _timeline = 0;

  public VkPhysicalDeviceProperties Properties;
  public const long FenceTimeout = 100000000000;

  public VkPhysicalDeviceFeatures Features { get; private set; }
  public List<VkUtf8String> DeviceExtensions { get; private set; } = [];

  public VulkanDevice(IWindow window) {
    _window = window;
    CreateInstance();
    CreateSurface();
    PickPhysicalDevice();
    CreateLogicalDevice();
    _commandPool = CreateCommandPool();

    /*
    VkSemaphoreCreateInfo createInfo = new();
    VkSemaphoreTypeCreateInfo typeInfo = new() {
      semaphoreType = VkSemaphoreType.Timeline,
      initialValue = 0
    };
    unsafe {
      createInfo.pNext = &typeInfo;
      _timeline = 0;

      vkCreateSemaphore(_logicalDevice, &createInfo, null, out _semaphore).CheckResult();
    }
    */
    // vkCreateSemaphore(_logicalDevice, out _semaphore).CheckResult();
  }

  public unsafe void CreateBuffer(
    ulong size,
    BufferUsage uFlags,
    MemoryProperty pFlags,
    out ulong buffer,
    out ulong bufferMemory
  ) {
    VkBufferCreateInfo bufferInfo = new() {
      size = size,
      usage = (VkBufferUsageFlags)uFlags,
      sharingMode = VkSharingMode.Exclusive
    };

    // Logger.Info($"Allocating Size: {size}");

    vkCreateBuffer(_logicalDevice, &bufferInfo, null, out var buff).CheckResult();
    buffer = buff;

    VkMemoryRequirements memRequirements;
    vkGetBufferMemoryRequirements(_logicalDevice, buffer, out memRequirements);

    VkMemoryAllocateInfo allocInfo = new() {
      allocationSize = memRequirements.size,
      memoryTypeIndex = FindMemoryType(memRequirements.memoryTypeBits, pFlags)
    };

    vkAllocateMemory(_logicalDevice, &allocInfo, null, out var buffMem).CheckResult();
    bufferMemory = buffMem;
    vkBindBufferMemory(_logicalDevice, buffer, bufferMemory, 0).CheckResult();
  }

  public unsafe void AllocateBuffer(
    ulong size,
    BufferUsage uFlags,
    MemoryProperty pFlags,
    ulong buffer,
    out ulong bufferMemory
  ) {
    VkMemoryRequirements memRequirements;
    vkGetBufferMemoryRequirements(_logicalDevice, buffer, out memRequirements);

    VkMemoryAllocateInfo allocInfo = new() {
      allocationSize = memRequirements.size,
      memoryTypeIndex = FindMemoryType(memRequirements.memoryTypeBits, pFlags)
    };

    vkAllocateMemory(_logicalDevice, &allocInfo, null, out var buffMem).CheckResult();
    bufferMemory = buffMem;
    vkBindBufferMemory(_logicalDevice, buffer, bufferMemory, 0).CheckResult();
  }

  public unsafe Task CopyBuffer(ulong srcBuffer, ulong dstBuffer, ulong size) {
    lock (_queueLock) {
      VkCommandBuffer commandBuffer = BeginSingleTimeCommands();
      // var commandBuffer = CreateCommandBuffer(VkCommandBufferLevel.Primary, true);

      VkBufferCopy copyRegion = new() {
        srcOffset = 0,  // Optional
        dstOffset = 0,  // Optional
        size = size
      };
      vkCmdCopyBuffer(commandBuffer, srcBuffer, dstBuffer, 1, &copyRegion);

      // FlushCommandBuffer(commandBuffer, _presentQueue, true);
      EndSingleTimeCommands(commandBuffer);
      return Task.CompletedTask;
    }
  }


  public unsafe VkCommandBuffer CreateCommandBuffer(VkCommandBufferLevel level, VkCommandPool commandPool, bool begin) {
    var allocInfo = new VkCommandBufferAllocateInfo();
    allocInfo.commandPool = commandPool;
    allocInfo.level = level;
    allocInfo.commandBufferCount = 1;

    var cmdBuffer = new VkCommandBuffer();
    vkAllocateCommandBuffers(_logicalDevice, &allocInfo, &cmdBuffer).CheckResult();
    if (begin) {
      var buffInfo = new VkCommandBufferBeginInfo();
      vkBeginCommandBuffer(cmdBuffer, &buffInfo).CheckResult();
    }

    return cmdBuffer;
  }

  public unsafe VkCommandBuffer CreateCommandBuffer(VkCommandBufferLevel level, bool begin) {
    return CreateCommandBuffer(level, _commandPool, begin);
  }

  public unsafe void FlushCommandBuffer(VkCommandBuffer cmdBuffer, VkQueue queue, VkCommandPool pool, bool free) {
    if (cmdBuffer == VkCommandBuffer.Null) return;

    vkEndCommandBuffer(cmdBuffer).CheckResult();

    var submitInfo = new VkSubmitInfo();
    submitInfo.commandBufferCount = 1;
    submitInfo.pCommandBuffers = &cmdBuffer;

    // Create fence to ensure that the command buffer has finished executing
    var fenceInfo = new VkFenceCreateInfo();
    fenceInfo.flags = VkFenceCreateFlags.None;
    vkCreateFence(_logicalDevice, &fenceInfo, null, out var fence).CheckResult();
    // Submit to the queue
    Application.Mutex.WaitOne();
    vkQueueSubmit(queue, submitInfo, fence).CheckResult();
    Application.Mutex.ReleaseMutex();
    vkWaitForFences(_logicalDevice, 1, &fence, VkBool32.True, 100000000000);
    vkDestroyFence(_logicalDevice, fence, null);
    if (free) {
      vkFreeCommandBuffers(_logicalDevice, pool, 1, &cmdBuffer);
    }
  }

  public void FlushCommandBuffer(VkCommandBuffer cmdBuffer, VkQueue queue, bool free) {
    FlushCommandBuffer(cmdBuffer, queue, _commandPool, free);
  }

  public static unsafe VkSemaphore CreateSemaphore(VulkanDevice device) {
    var semaphoreInfo = new VkSemaphoreCreateInfo();
    var semaphore = new VkSemaphore();
    vkCreateSemaphore(device.LogicalDevice, &semaphoreInfo, null, &semaphore);
    return semaphore;
  }

  public static unsafe void DestroySemaphore(VulkanDevice device, VkSemaphore semaphore) {
    vkDestroySemaphore(device.LogicalDevice, semaphore, null);
  }

  public VkFormat FindSupportedFormat(List<VkFormat> candidates, VkImageTiling tilling, VkFormatFeatureFlags features) {
    foreach (var format in candidates) {
      vkGetPhysicalDeviceFormatProperties(_physicalDevice, format, out VkFormatProperties props);

      if (tilling == VkImageTiling.Linear && (props.linearTilingFeatures & features) == features) {
        return format;
      } else if (tilling == VkImageTiling.Optimal && (props.optimalTilingFeatures & features) == features) {
        return format;
      }
    }
    throw new Exception("failed to find candidate!");
  }

  internal unsafe void CreateImageWithInfo(
    VkImageCreateInfo imageInfo,
    VkMemoryPropertyFlags properties,
    out VkImage image,
    out VkDeviceMemory imageMemory
  ) {

    vkCreateImage(_logicalDevice, &imageInfo, null, out image).CheckResult();

    VkMemoryRequirements memRequirements;
    vkGetImageMemoryRequirements(_logicalDevice, image, out memRequirements);

    VkMemoryAllocateInfo allocInfo = new() {
      allocationSize = memRequirements.size,
      memoryTypeIndex = FindMemoryType(memRequirements.memoryTypeBits, (MemoryProperty)properties)
    };

    vkAllocateMemory(_logicalDevice, &allocInfo, null, out imageMemory).CheckResult();
    vkBindImageMemory(_logicalDevice, image, imageMemory, 0);
  }

  public uint FindMemoryType(uint typeFilter, MemoryProperty properties) {
    VkPhysicalDeviceMemoryProperties memProperties;
    vkGetPhysicalDeviceMemoryProperties(_physicalDevice, out memProperties);
    for (int i = 0; i < memProperties.memoryTypeCount; i++) {
      // 1 << n is basically an equivalent to 2^n.
      // if ((typeFilter & (1 << i)) &&

      if (((MemoryProperty)memProperties.memoryTypes[i].propertyFlags & properties) == properties) {
        return (uint)i;
      }

      //if ((typeFilter & (1 << 1)) != 0 && (memProperties.memoryTypes[i].propertyFlags & properties) == properties) {
      //  return (uint)i;
      //}
    }

    throw new Exception($"Failed to find suitable memory type");
  }

  public unsafe IntPtr BeginSingleTimeCommands() {
    lock (_queueLock) {
      VkCommandBufferAllocateInfo allocInfo = new() {
        level = VkCommandBufferLevel.Primary,
        commandPool = _commandPool,
        commandBufferCount = 1
      };

      VkCommandBuffer commandBuffer;
      vkAllocateCommandBuffers(_logicalDevice, &allocInfo, &commandBuffer);

      VkCommandBufferBeginInfo beginInfo = new() {
        flags = VkCommandBufferUsageFlags.OneTimeSubmit
      };

      vkBeginCommandBuffer(commandBuffer, &beginInfo);
      return commandBuffer;
    }
  }

  public unsafe void EndSingleTimeCommands(IntPtr commandBuffer) {
    lock (_queueLock) {
      vkEndCommandBuffer(commandBuffer);

      SubmitQueue(commandBuffer);

      // vkFreeCommandBuffers(_logicalDevice, _commandPool, 1, &commandBuffer);
      vkFreeCommandBuffers(_logicalDevice, _commandPool, commandBuffer);
    }
  }

  public unsafe void WaitDevice() {
    var result = vkDeviceWaitIdle(_logicalDevice);
    if (result == VkResult.ErrorDeviceLost) {
      throw new VkException($"[DWARF] Device Lost! {result}");
    }
  }

  public unsafe VkFence CreateFence(VkFenceCreateFlags flags = VkFenceCreateFlags.None) {
    var fenceInfo = new VkFenceCreateInfo();
    fenceInfo.flags = flags;
    vkCreateFence(_logicalDevice, &fenceInfo, null, out var fence).CheckResult();
    return fence;
  }

  public unsafe void WaitQueue(VkQueue queue) {
    vkQueueWaitIdle(queue);
  }

  public void WaitQueue() {
    // WaitQueue(_graphicsQueue);
    WaitAllQueues();
  }

  public void WaitAllQueues() {
    WaitQueue(_graphicsQueue);
    // WaitQueue(_presentQueue);
  }

  private unsafe void SubmitQueue(VkCommandBuffer commandBuffer) {
    VkSubmitInfo submitInfo = new() {
      commandBufferCount = 1,
      pCommandBuffers = &commandBuffer,
    };

    var fence = CreateFence(VkFenceCreateFlags.None);
    SubmitQueue(1, &submitInfo, fence, true);
  }

  // private void SubmitQueue(VkCommandBuffer commandBuffer) {
  //   SubmitQueue(_graphicsQueue, commandBuffer);
  // }

  public unsafe void SubmitQueue(uint submitCount, VkSubmitInfo* pSubmits, VkFence fence, bool destroy = false) {
    vkQueueSubmit(_graphicsQueue, submitCount, pSubmits, fence).CheckResult();
    vkWaitForFences(_logicalDevice, 1, &fence, VkBool32.True, UInt64.MaxValue);
    if (destroy) {
      vkDestroyFence(_logicalDevice, fence);
    }
  }

  public unsafe void SubmitQueue2(uint submitCount, VkSubmitInfo2* pSubmits, VkFence fence, bool destroy = false) {
    try {
      Application.Mutex.WaitOne();
      vkQueueSubmit2(_graphicsQueue, submitCount, pSubmits, fence).CheckResult();
      vkWaitForFences(_logicalDevice, 1, &fence, VkBool32.True, UInt64.MaxValue);
      if (destroy) {
        vkDestroyFence(_logicalDevice, fence);
      }
    } finally {
      Application.Mutex.ReleaseMutex();
    }
  }

  private unsafe void SubmitSemaphore() {
    var timelineCreateInfo = new VkSemaphoreTypeCreateInfo();
    timelineCreateInfo.pNext = null;
    timelineCreateInfo.semaphoreType = VkSemaphoreType.Timeline;
    timelineCreateInfo.initialValue = 0;

    var createInfo = new VkSemaphoreCreateInfo();
    createInfo.pNext = &timelineCreateInfo;
    createInfo.flags = VkSemaphoreCreateFlags.None;

    vkCreateSemaphore(_logicalDevice, &createInfo, null, out var timelineSemaphore).CheckResult();

    ulong waitValue = 2; // Wait until semaphore value is >= 2
    ulong signalValue = 3; // Set semaphore value to 3

    VkTimelineSemaphoreSubmitInfo timelineInfo = new();
    timelineInfo.pNext = null;
    timelineInfo.waitSemaphoreValueCount = 1;
    timelineInfo.pWaitSemaphoreValues = &waitValue;
    timelineInfo.signalSemaphoreValueCount = 1;
    timelineInfo.pSignalSemaphoreValues = &signalValue;

    VkSubmitInfo submitInfo = new();
    submitInfo.pNext = &timelineInfo;
    submitInfo.waitSemaphoreCount = 1;
    submitInfo.pWaitSemaphores = &timelineSemaphore;
    submitInfo.signalSemaphoreCount = 1;
    submitInfo.pSignalSemaphores = &timelineSemaphore;
    submitInfo.commandBufferCount = 0;
    submitInfo.pCommandBuffers = null;

    vkQueueSubmit(GraphicsQueue, submitInfo, VkFence.Null);
  }

  private unsafe void CreateInstance() {
    HashSet<VkUtf8String> availableInstanceLayers = [.. DeviceHelper.EnumerateInstanceLayers()];
    HashSet<VkUtf8String> availableInstanceExtensions = [.. DeviceHelper.GetInstanceExtensions()];

    var appInfo = new VkApplicationInfo {
      pApplicationName = AppName,
      applicationVersion = new(1, 0, 0),
      pEngineName = EngineName,
      engineVersion = new(1, 0, 0),
      apiVersion = VkVersion.Version_1_4
    };

    var createInfo = new VkInstanceCreateInfo {
      pApplicationInfo = &appInfo
    };

    List<VkUtf8String> instanceExtensions = [];
    foreach (var ext in SDL_Vulkan_GetInstanceExtensions()) {
      ReadOnlySpan<byte> sdlExtSpan = Encoding.UTF8.GetBytes(ext);
      instanceExtensions.Add(sdlExtSpan);
    }

    List<VkUtf8String> instanceLayers = new();
    // Check if VK_EXT_debug_utils is supported, which supersedes VK_EXT_Debug_Report
    foreach (VkUtf8String availableExtension in availableInstanceExtensions) {
      if (availableExtension == VK_EXT_DEBUG_UTILS_EXTENSION_NAME) {
        instanceExtensions.Add(VK_EXT_DEBUG_UTILS_EXTENSION_NAME);
      } else if (availableExtension == VK_EXT_SWAPCHAIN_COLOR_SPACE_EXTENSION_NAME) {
        instanceExtensions.Add(VK_EXT_SWAPCHAIN_COLOR_SPACE_EXTENSION_NAME);
      }

      if (availableExtension == VK_EXT_SWAPCHAIN_MAINTENANCE_1_EXTENSION_NAME) {
        instanceExtensions.Add(VK_EXT_SWAPCHAIN_MAINTENANCE_1_EXTENSION_NAME);
      }

      if (availableExtension == VK_EXT_SURFACE_MAINTENANCE_1_EXTENSION_NAME) {
        instanceExtensions.Add(VK_EXT_SURFACE_MAINTENANCE_1_EXTENSION_NAME);
      }

      if (availableExtension == VK_KHR_GET_SURFACE_CAPABILITIES_2_EXTENSION_NAME) {
        instanceExtensions.Add(VK_KHR_GET_SURFACE_CAPABILITIES_2_EXTENSION_NAME);
      }

      if (availableExtension == VK_EXT_ROBUSTNESS_2_EXTENSION_NAME) {
        instanceExtensions.Add(VK_EXT_ROBUSTNESS_2_EXTENSION_NAME);
      }
    }
    // instanceExtensions.Add(VK_EXT_PIPELINE_CREATION_CACHE_CONTROL_EXTENSION_NAME);
    // instanceExtensions.Add(VK_EXT_dynamic_rendering_unused_attachments);
    // instanceExtensions.Add(VK_EXT_DYNAMIC_RENDERING_UNUSED_ATTACHMENTS_EXTENSION_NAME);
    // instanceExtensions.Add(VK_EXT_dynamic_rendering_unused_attachments)

    if (s_EnableValidationLayers) {
      DeviceHelper.GetOptimalValidationLayers(availableInstanceLayers, instanceLayers);
    }

    using VkStringArray vkLayerNames = new(instanceLayers);
    using VkStringArray vkInstanceExtensions = new(instanceExtensions);

    createInfo.enabledLayerCount = vkLayerNames.Length;
    createInfo.ppEnabledLayerNames = vkLayerNames;
    createInfo.enabledExtensionCount = vkInstanceExtensions.Length;
    createInfo.ppEnabledExtensionNames = vkInstanceExtensions;

    var debugCreateInfo = new VkDebugUtilsMessengerCreateInfoEXT();

    if (instanceLayers.Count > 0 && s_EnableValidationLayers) {
      debugCreateInfo = SetupDebugCallbacks();
      createInfo.pNext = &debugCreateInfo;
    } else {
      createInfo.pNext = null;
    }

    var result = vkCreateInstance(&createInfo, null, out _vkInstance);
    if (result != VkResult.Success) throw new Exception("Failed to create instance!");

    vkLoadInstanceOnly(_vkInstance);

    if (instanceLayers.Count > 0) {
      vkCreateDebugUtilsMessengerEXT(_vkInstance, &debugCreateInfo, null, out _debugMessenger).CheckResult();
    }
  }

  private unsafe VkDebugUtilsMessengerCreateInfoEXT SetupDebugCallbacks() {
    Logger.Info("Creating Debug Callbacks...");
    var createInfo = new VkDebugUtilsMessengerCreateInfoEXT {
      messageSeverity =
        VkDebugUtilsMessageSeverityFlagsEXT.Error |
        VkDebugUtilsMessageSeverityFlagsEXT.Warning,
      messageType =
        VkDebugUtilsMessageTypeFlagsEXT.General |
        VkDebugUtilsMessageTypeFlagsEXT.Validation |
        VkDebugUtilsMessageTypeFlagsEXT.Performance,
      pfnUserCallback = &DebugMessengerCallback,
      pUserData = null
    };

    return createInfo;
  }

  [UnmanagedCallersOnly]
  private static unsafe uint DebugMessengerCallback(VkDebugUtilsMessageSeverityFlagsEXT messageSeverity,
    VkDebugUtilsMessageTypeFlagsEXT messageTypes,
    VkDebugUtilsMessengerCallbackDataEXT* pCallbackData,
    void* userData
  ) {
    VkUtf8String message = new(pCallbackData->pMessage);
    var msg = message.ToString();
    if (messageTypes == VkDebugUtilsMessageTypeFlagsEXT.Validation) {
      if (messageSeverity == VkDebugUtilsMessageSeverityFlagsEXT.Error) {
        Logger.Error($"[Vulkan]: Validation: {messageSeverity} - {message}");
      } else if (messageSeverity == VkDebugUtilsMessageSeverityFlagsEXT.Warning) {
        Logger.Warn($"[Vulkan]: Validation: {messageSeverity} - {message}");
      }

      Debug.WriteLine($"[Vulkan]: Validation: {messageSeverity} - {message}");
    } else {
      if (messageSeverity == VkDebugUtilsMessageSeverityFlagsEXT.Error) {
        Logger.Error($"[Vulkan]: {messageSeverity} - {message}");
      } else if (messageSeverity == VkDebugUtilsMessageSeverityFlagsEXT.Warning) {
        Logger.Warn($"[Vulkan]: {messageSeverity} - {message}");
      }

      Debug.WriteLine($"[Vulkan]: {messageSeverity} - {message}");
    }

    return VK_FALSE;
  }

  private unsafe void CreateSurface() {
    Surface = _window.CreateSurface(_vkInstance.Handle);
  }

  private unsafe void PickPhysicalDevice() {
    _physicalDevice = DeviceHelper.GetPhysicalDevice(_vkInstance, Surface);
  }

  private unsafe void CreateLogicalDevice() {
    vkGetPhysicalDeviceProperties(_physicalDevice, out Properties);
    var queueFamilies = DeviceHelper.FindQueueFamilies(_physicalDevice, Surface);
    var availableDeviceExtensions = vkEnumerateDeviceExtensionProperties(_physicalDevice);

    HashSet<uint> uniqueQueueFamilies = [queueFamilies.graphicsFamily];

    float priority = 1.0f;
    uint queueCount = 0;
    VkDeviceQueueCreateInfo[] queueCreateInfos = new VkDeviceQueueCreateInfo[2];

    foreach (uint queueFamily in uniqueQueueFamilies) {
      VkDeviceQueueCreateInfo queueCreateInfo = new() {
        queueFamilyIndex = queueFamily,
        queueCount = 1,
        pQueuePriorities = &priority,
        flags = VkDeviceQueueCreateFlags.None
      };

      queueCreateInfos[queueCount++] = queueCreateInfo;
    }

    VkPhysicalDeviceFeatures deviceFeatures = new() {
      samplerAnisotropy = true,
      fillModeNonSolid = true,
      alphaToOne = true,
      sampleRateShading = true,
      multiDrawIndirect = true,
      geometryShader = true,
      robustBufferAccess = true,
      shaderStorageBufferArrayDynamicIndexing = true,
      independentBlend = true,
      depthClamp = true,
    };


    VkPhysicalDeviceVulkan11Features vk11Features = new() {
      shaderDrawParameters = true,
    };

    VkPhysicalDeviceVulkan12Features vk12Features = new() {
      timelineSemaphore = true,
      descriptorIndexing = true,
      descriptorBindingPartiallyBound = true,
      descriptorBindingVariableDescriptorCount = true,
      runtimeDescriptorArray = true,
      descriptorBindingSampledImageUpdateAfterBind = true,
      descriptorBindingUpdateUnusedWhilePending = true,
      descriptorBindingStorageBufferUpdateAfterBind = true,
      descriptorBindingUniformBufferUpdateAfterBind = true,
      pNext = &vk11Features,
    };

    VkPhysicalDeviceVulkan13Features vk13Features = new() {
      synchronization2 = true,
      dynamicRendering = true,
      inlineUniformBlock = true,
      pNext = &vk12Features,
    };

    VkPhysicalDeviceVulkan14Features vk14Features = new() {
      hostImageCopy = true,
      pushDescriptor = true,
      // dynamicRenderingLocalRead = true,
      pNext = &vk13Features
    };

    // VkPhysicalDeviceDynamicRenderingUnusedAttachmentsFeaturesEXT unusedAttachmentsFeaturesEXT = new() {
    //   dynamicRenderingUnusedAttachments = true,
    //   pNext = &vk14Features
    // };

    VkPhysicalDeviceRobustness2FeaturesEXT physicalDeviceRobustness = new() {
      nullDescriptor = true,
      pNext = &vk14Features
    };

    VkPhysicalDeviceFeatures2 deviceFeatures2 = new() {
      features = deviceFeatures,
      pNext = &physicalDeviceRobustness
    };


    VkDeviceCreateInfo createInfo = new() {
      queueCreateInfoCount = queueCount,
      pNext = &deviceFeatures2
    };

    fixed (VkDeviceQueueCreateInfo* ptr = queueCreateInfos) {
      createInfo.pQueueCreateInfos = ptr;
    }

    List<VkUtf8String> enabledExtensions;
    enabledExtensions = [
      VK_KHR_SWAPCHAIN_EXTENSION_NAME,
      VK_EXT_SWAPCHAIN_MAINTENANCE_1_EXTENSION_NAME,
      VK_EXT_ROBUSTNESS_2_EXTENSION_NAME
      // VK_EXT_DYNAMIC_RENDERING_UNUSED_ATTACHMENTS_EXTENSION_NAME,
      // VK_KHR_DYNAMIC_RENDERING_LOCAL_READ_EXTENSION_NAME
    ];

    using var deviceExtensionNames = new VkStringArray(enabledExtensions);

    // createInfo.pEnabledFeatures = &deviceFeatures;
    createInfo.pNext = &deviceFeatures2;
    createInfo.enabledExtensionCount = deviceExtensionNames.Length;
    createInfo.ppEnabledExtensionNames = deviceExtensionNames;

    Features = deviceFeatures2.features;
    DeviceExtensions = enabledExtensions;

    var result = vkCreateDevice(_physicalDevice, &createInfo, null, out _logicalDevice);
    if (result != VkResult.Success) throw new Exception($"Failed to create a device! [{result}]");

    vkLoadDevice(_logicalDevice);

    vkGetDeviceQueue(_logicalDevice, queueFamilies.graphicsFamily, 0, out _graphicsQueue);
    // vkGetDeviceQueue(_logicalDevice, queueFamilies.presentFamily, 0, out _presentQueue);
  }

  public unsafe ulong CreateCommandPool() {
    var queueFamilies = DeviceHelper.FindQueueFamilies(_physicalDevice, Surface);

    VkCommandPoolCreateInfo poolCreateInfo = new() {
      queueFamilyIndex = queueFamilies.graphicsFamily,
      flags = VkCommandPoolCreateFlags.Transient | VkCommandPoolCreateFlags.ResetCommandBuffer
    };

    var result = vkCreateCommandPool(_logicalDevice, &poolCreateInfo, null, out var commandPool);
    return result != VkResult.Success ? throw new Exception("Failed to create command pool!") : (ulong)commandPool;
  }

  public unsafe void DisposeCommandPool(ulong commandPool) {
    vkDestroyCommandPool(LogicalDevice, commandPool, null);
  }

  public object CreateFence(FenceCreateFlags fenceCreateFlags) {
    return CreateFence((VkFenceCreateFlags)fenceCreateFlags);
  }

  public void WaitFence(object fence, bool waitAll) {
    vkWaitForFences(LogicalDevice, (VkFence)fence, waitAll, VulkanDevice.FenceTimeout);
    unsafe {
      vkDestroyFence(LogicalDevice, (VkFence)fence);
    }
  }

  public void BeginWaitFence(object fence, bool waitAll) {
    vkWaitForFences(LogicalDevice, (VkFence)fence, waitAll, FenceTimeout);
  }
  public unsafe void EndWaitFence(object fence) {
    vkDestroyFence(LogicalDevice, (VkFence)fence);
  }

  public unsafe void Dispose() {
    vkDestroyCommandPool(_logicalDevice, _commandPool);
    vkDestroyDevice(_logicalDevice);
    vkDestroySurfaceKHR(_vkInstance, Surface);
    vkDestroyDebugUtilsMessengerEXT(_vkInstance, _debugMessenger);
    vkDestroyInstance(_vkInstance);
  }

  public IntPtr LogicalDevice => _logicalDevice;
  public IntPtr PhysicalDevice => _physicalDevice;
  public ulong Surface { get; private set; } = VkSurfaceKHR.Null;
  public ulong MinStorageBufferOffsetAlignment => Properties.limits.minStorageBufferOffsetAlignment;
  public ulong MinUniformBufferOffsetAlignment => Properties.limits.minUniformBufferOffsetAlignment;

  public VkUtf8String AppName = "Dwarf App"u8;
  public VkUtf8String EngineName = "Dwarf Engine"u8;

  public ulong CommandPool {
    get {
      lock (_commandPoolLock) {
        return _commandPool;
      }
    }
  }

  public IntPtr GraphicsQueue {
    get { return _graphicsQueue; }
  }

  public IntPtr PresentQueue {
    // get { return _presentQueue; }
    get { return IntPtr.Zero; }
  }

  public VkInstance VkInstance => _vkInstance;
}