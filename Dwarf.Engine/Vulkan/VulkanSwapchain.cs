using System.Runtime.InteropServices;
using Dwarf.AbstractionLayer;
using Dwarf.Extensions.Logging;
using Dwarf.Pathfinding;
using Dwarf.Utils;
using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Vulkan;

public class VulkanSwapchain : IDisposable {
  private const int MAX_FRAMES_IN_FLIGHT = 2;

  private readonly VulkanDevice _device;
  private VkSwapchainKHR _handle = VkSwapchainKHR.Null;
  private VkImageView[] _swapChainImageViews = null!;
  private VkImage[] _swapchainImages = [];
  private VkRenderPass _renderPass = VkRenderPass.Null;
  private VkRenderPass _postProcessPass = VkRenderPass.Null;

  private VkImageView[] _colorImageViews = [];
  private VkImage[] _colorImages = [];
  private VkSampler _colorSampler = VkSampler.Null;
  private VkDeviceMemory[] _colorImageMemories = [];

  private VkImage[] _depthImages = [];
  private VkSampler _depthSampler = VkSampler.Null;
  private VkDeviceMemory[] _depthImagesMemories = [];
  private VkImageView[] _depthImageViews = [];

  private readonly VkImage[] _normalImages = [];
  private readonly VkDeviceMemory[] _normalImagesMemories = [];
  private readonly VkImageView[] _normalImageViews = [];

  private VkFormat _swapchainImageFormat = VkFormat.Undefined;
  private VkFormat _swapchainDepthFormat = VkFormat.Undefined;
  private VkFormat _colorImageFormat = VkFormat.Undefined;

  private VkExtent2D _swapchainExtent = VkExtent2D.Zero;
  private VkFramebuffer[] _swapchainFramebuffers = [];
  private VkFramebuffer[] _postProcessFramebuffers = [];
  private unsafe VkSemaphore* _imageAvailableSemaphores;
  private unsafe VkSemaphore* _renderFinishedSemaphores;
  private unsafe VkFence* _inFlightFences;
  private unsafe VkFence* _imagesInFlight;

  // private Swapchain _oldSwapchain = null!;
  private VulkanDescriptorPool _descriptorPool = null!;
  private VulkanDescriptorSetLayout _inputAttachmentsLayout = null!;
  private VulkanDescriptorSetLayout _postProcessLayout = null!;
  // private VkDescriptorSet[] _colorDescriptors;
  // private VkDescriptorSet[] _depthDescriptors;
  private VkDescriptorSet[] _imageDescriptors = [];
  private VkDescriptorSet[] _postProcessDescriptors = [];

  private int _currentFrame = 0;
  private uint _imageIndex = 0;
  private int _previousFrame = -1;

  private readonly Lock _swapchainLock = new();

  public VulkanSwapchain(VulkanDevice device, VkExtent2D extent) {
    _device = device;
    Extent2D = extent;

    Init();
  }

  /*
  public Swapchain(Device device, VkExtent2D extent, ref Swapchain previous) {
    _device = device;
    _extent = extent;
    _oldSwapchain = previous;

    Init();

    _oldSwapchain?.Dispose();
    _oldSwapchain = null!;
  }
  */

  public bool CompareSwapFormats(VulkanSwapchain swapchain) {
    return swapchain._swapchainDepthFormat == _swapchainDepthFormat &&
           swapchain._swapchainImageFormat == _swapchainImageFormat;
  }

  private void Init() {
    CreateSwapChain();
    CreateImageViews();
    CreateSamplers();
    // CreateRenderPass();
    // CreatePostProcessRenderPass();
    CreateDepthResources();
    CreateColorResources();
    CreateDescriptors();
    // CreateFramebuffers();
    CreateSyncObjects();
  }

  private unsafe void CreateSwapChain() {
    SwapChainSupportDetails swapChainSupport = VkUtils.QuerySwapChainSupport(_device.PhysicalDevice, _device.Surface);

    // TODO : Ducktape solution, prevents from crashing
    if (swapChainSupport.Capabilities.maxImageExtent.width < 1)
      swapChainSupport.Capabilities.maxImageExtent.width = Extent2D.width;

    if (swapChainSupport.Capabilities.maxImageExtent.height < 1)
      swapChainSupport.Capabilities.maxImageExtent.height = Extent2D.height;

    VkSurfaceFormatKHR surfaceFormat = ChooseSwapSurfaceFormat(swapChainSupport.Formats);
    SurfaceFormat = surfaceFormat.format;
    DepthFormat = FindDepthFormat();
    VkPresentModeKHR presentMode = ChooseSwapPresentMode(swapChainSupport.PresentModes);
    var extent = ChooseSwapExtent(swapChainSupport.Capabilities);

    uint imageCount = swapChainSupport.Capabilities.minImageCount + 1;
    if (swapChainSupport.Capabilities.maxImageCount > 0 &&
        imageCount > swapChainSupport.Capabilities.maxImageCount) {
      imageCount = swapChainSupport.Capabilities.maxImageCount;
    }

    var createInfo = new VkSwapchainCreateInfoKHR {
      surface = _device.Surface,

      minImageCount = imageCount,
      imageFormat = surfaceFormat.format,
      imageColorSpace = surfaceFormat.colorSpace,
      imageExtent = extent,
      imageArrayLayers = 1,
      imageUsage = VkImageUsageFlags.ColorAttachment | VkImageUsageFlags.TransferSrc | VkImageUsageFlags.TransferDst
    };

    var queueFamilies = DeviceHelper.FindQueueFamilies(_device.PhysicalDevice, _device.Surface);

    if (queueFamilies.graphicsFamily != queueFamilies.presentFamily) {
      uint* indices = stackalloc uint[2];
      indices[0] = queueFamilies.graphicsFamily;
      indices[1] = queueFamilies.presentFamily;

      createInfo.imageSharingMode = VkSharingMode.Concurrent;
      createInfo.queueFamilyIndexCount = 2;
      createInfo.pQueueFamilyIndices = indices;
    } else {
      createInfo.imageSharingMode = VkSharingMode.Exclusive;
      createInfo.queueFamilyIndexCount = 0;
      createInfo.pQueueFamilyIndices = null;
    }

    createInfo.preTransform = swapChainSupport.Capabilities.currentTransform;
    createInfo.compositeAlpha = VkCompositeAlphaFlagsKHR.Opaque;
    createInfo.presentMode = presentMode;
    createInfo.clipped = true;
    createInfo.oldSwapchain = VkSwapchainKHR.Null;

    /*
    if (_oldSwapchain == null) {
    } else {
      createInfo.oldSwapchain = _oldSwapchain.Handle;
    }
    */

    var result = vkCreateSwapchainKHR(_device.LogicalDevice, &createInfo, null, out _handle);
    if (result != VkResult.Success) throw new Exception("Error while creating swapchain!");

    uint c = imageCount;
    vkGetSwapchainImagesKHR(_device.LogicalDevice, _handle, &c, null);

    VkImage[] imgs = new VkImage[c];
    fixed (VkImage* ptr = imgs) {
      vkGetSwapchainImagesKHR(_device.LogicalDevice, _handle, &c, ptr);
      _swapchainImages = imgs;
    }

    _swapchainImageFormat = surfaceFormat.format;
    _swapchainExtent = extent;

    Logger.Info("Successfully created Swapchain");
  }

  private unsafe void CreateImageViews() {
    ReadOnlySpan<VkImage> swapChainImages = vkGetSwapchainImagesKHR(_device.LogicalDevice, _handle);
    _swapChainImageViews = new VkImageView[swapChainImages.Length];

    SwapChainSupportDetails swapChainSupport = VkUtils.QuerySwapChainSupport(_device.PhysicalDevice, _device.Surface);
    VkSurfaceFormatKHR surfaceFormat = ChooseSwapSurfaceFormat(swapChainSupport.Formats);
    // VkPresentModeKHR presentMode = ChooseSwapPresentMode(swapChainSupport.PresentModes);
    // var extent = ChooseSwapExtent(swapChainSupport.Capabilities);

    for (int i = 0; i < swapChainImages.Length; i++) {
      VkImageViewCreateInfo colorAttachmentView = new() {
        pNext = null,
        format = surfaceFormat.format,
        components = new() {
          r = VK_COMPONENT_SWIZZLE_R,
          g = VK_COMPONENT_SWIZZLE_G,
          b = VK_COMPONENT_SWIZZLE_B,
          a = VK_COMPONENT_SWIZZLE_A
        },
        subresourceRange = new() {
          aspectMask = VK_IMAGE_ASPECT_COLOR_BIT,
          baseMipLevel = 0,
          levelCount = 1,
          baseArrayLayer = 0,
          layerCount = 1,
        },
        viewType = VK_IMAGE_VIEW_TYPE_2D,
        flags = 0,
        image = swapChainImages[i]
      };
      // var viewCreateInfo = new VkImageViewCreateInfo(
      //     swapChainImages[i],
      //     VkImageViewType.Image2D,
      //     surfaceFormat.format,
      //     VkComponentMapping.Rgba,
      //     new VkImageSubresourceRange(VkImageAspectFlags.Color, 0, 1, 0, 1)
      // );

      vkCreateImageView(_device.LogicalDevice, &colorAttachmentView, null, out _swapChainImageViews[i]).CheckResult();
    }
  }

  private unsafe void CreateRenderPass() {
    SwapChainSupportDetails swapChainSupport = VkUtils.QuerySwapChainSupport(_device.PhysicalDevice, _device.Surface);
    VkSurfaceFormatKHR surfaceFormat = ChooseSwapSurfaceFormat(swapChainSupport.Formats);
    VkPresentModeKHR presentMode = ChooseSwapPresentMode(swapChainSupport.PresentModes);

    VkAttachmentDescription* attachments = stackalloc VkAttachmentDescription[3];
    attachments[0] = new() {
      format = surfaceFormat.format,
      samples = VK_SAMPLE_COUNT_1_BIT,
      loadOp = VK_ATTACHMENT_LOAD_OP_CLEAR,
      storeOp = VK_ATTACHMENT_STORE_OP_STORE,
      stencilLoadOp = VK_ATTACHMENT_LOAD_OP_DONT_CARE,
      stencilStoreOp = VK_ATTACHMENT_STORE_OP_DONT_CARE,
      initialLayout = VK_IMAGE_LAYOUT_UNDEFINED,
      finalLayout = VK_IMAGE_LAYOUT_PRESENT_SRC_KHR
    };
    attachments[1] = new() {
      format = surfaceFormat.format,
      samples = VK_SAMPLE_COUNT_1_BIT,
      loadOp = VK_ATTACHMENT_LOAD_OP_CLEAR,
      storeOp = VK_ATTACHMENT_STORE_OP_STORE,
      stencilLoadOp = VK_ATTACHMENT_LOAD_OP_DONT_CARE,
      stencilStoreOp = VK_ATTACHMENT_STORE_OP_DONT_CARE,
      initialLayout = VK_IMAGE_LAYOUT_UNDEFINED,
      finalLayout = VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL
    };
    attachments[2] = new() {
      format = FindDepthFormat(),
      samples = VK_SAMPLE_COUNT_1_BIT,
      loadOp = VK_ATTACHMENT_LOAD_OP_CLEAR,
      storeOp = VK_ATTACHMENT_STORE_OP_STORE,
      stencilLoadOp = VK_ATTACHMENT_LOAD_OP_DONT_CARE,
      stencilStoreOp = VK_ATTACHMENT_STORE_OP_DONT_CARE,
      initialLayout = VK_IMAGE_LAYOUT_UNDEFINED,
      finalLayout = VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL
    };

    VkSubpassDescription* subpassDescription = stackalloc VkSubpassDescription[2];

    // first subpass

    {
      VkAttachmentReference colorReference = new() {
        attachment = 1,
        layout = VkImageLayout.AttachmentOptimal
      };

      VkAttachmentReference depthReference = new() {
        attachment = 2,
        layout = VkImageLayout.AttachmentOptimal
      };

      subpassDescription[0] = new() {
        pipelineBindPoint = VK_PIPELINE_BIND_POINT_GRAPHICS,
        colorAttachmentCount = 1,
        pColorAttachments = &colorReference,
        pDepthStencilAttachment = &depthReference
      };
    }

    // second subpass

    {
      VkAttachmentReference colorReferenceSwapchain = new() {
        attachment = 0,
        layout = VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL
      };

      VkAttachmentReference* inputReferences = stackalloc VkAttachmentReference[2];
      inputReferences[0] = new() {
        attachment = 1,
        layout = VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL
      };
      inputReferences[1] = new() {
        attachment = 2,
        layout = VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL
      };

      subpassDescription[1] = new() {
        pipelineBindPoint = VK_PIPELINE_BIND_POINT_GRAPHICS,
        colorAttachmentCount = 1,
        pColorAttachments = &colorReferenceSwapchain,
        inputAttachmentCount = 2,
        pInputAttachments = inputReferences
      };
    }

    VkSubpassDependency* dependencies = stackalloc VkSubpassDependency[3];
    dependencies[0] = new() {
      srcSubpass = VK_SUBPASS_EXTERNAL,
      dstSubpass = 0,
      srcStageMask = VK_PIPELINE_STAGE_BOTTOM_OF_PIPE_BIT,
      dstStageMask = VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT | VK_PIPELINE_STAGE_EARLY_FRAGMENT_TESTS_BIT,
      srcAccessMask = VK_ACCESS_MEMORY_READ_BIT,
      dstAccessMask = VK_ACCESS_COLOR_ATTACHMENT_READ_BIT | VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT | VK_ACCESS_DEPTH_STENCIL_ATTACHMENT_WRITE_BIT,
      dependencyFlags = VK_DEPENDENCY_BY_REGION_BIT,
    };
    dependencies[1] = new() {
      srcSubpass = 0,
      dstSubpass = 1,
      srcStageMask = VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT,
      dstStageMask = VK_PIPELINE_STAGE_FRAGMENT_SHADER_BIT,
      srcAccessMask = VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT,
      dstAccessMask = VK_ACCESS_SHADER_READ_BIT,
      dependencyFlags = VK_DEPENDENCY_BY_REGION_BIT,
    };
    dependencies[2] = new() {
      srcSubpass = 0,
      dstSubpass = VK_SUBPASS_EXTERNAL,
      srcStageMask = VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT,
      dstStageMask = VK_PIPELINE_STAGE_BOTTOM_OF_PIPE_BIT,
      srcAccessMask = VK_ACCESS_COLOR_ATTACHMENT_READ_BIT | VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT,
      dstAccessMask = VK_ACCESS_MEMORY_READ_BIT,
      dependencyFlags = VK_DEPENDENCY_BY_REGION_BIT,
    };

    VkRenderPassCreateInfo renderPassCreateInfo = new() {
      attachmentCount = 3,
      pAttachments = attachments,
      subpassCount = 2,
      pSubpasses = subpassDescription,
      dependencyCount = 3,
      pDependencies = dependencies
    };

    vkCreateRenderPass(_device.LogicalDevice, renderPassCreateInfo, null, out _renderPass).CheckResult();
  }

  private unsafe void CreatePostProcessRenderPass_Old() {
    VkSubpassDescription vkSubpassDescription = new() {
      pipelineBindPoint = VkPipelineBindPoint.Graphics
    };

    VkRenderPassCreateInfo renderPassCreateInfo = new() {
      attachmentCount = 0,
      subpassCount = 1,
      pSubpasses = &vkSubpassDescription
    };

    VkSubpassDependency* dependencies = stackalloc VkSubpassDependency[1];
    dependencies[0] = new() {
      srcSubpass = VK_SUBPASS_EXTERNAL,
      dstSubpass = 0,
      srcStageMask = VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT,
      srcAccessMask = VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT,
      dstStageMask = VK_PIPELINE_STAGE_FRAGMENT_SHADER_BIT,
      dstAccessMask = VK_ACCESS_SHADER_READ_BIT
    };

    renderPassCreateInfo.dependencyCount = 1;
    renderPassCreateInfo.pDependencies = dependencies;

    vkCreateRenderPass(_device.LogicalDevice, renderPassCreateInfo, null, out _postProcessPass).CheckResult();
  }

  private unsafe void CreatePostProcessRenderPass() {
    SwapChainSupportDetails swapChainSupport = VkUtils.QuerySwapChainSupport(_device.PhysicalDevice, _device.Surface);
    VkSurfaceFormatKHR surfaceFormat = ChooseSwapSurfaceFormat(swapChainSupport.Formats);
    VkPresentModeKHR presentMode = ChooseSwapPresentMode(swapChainSupport.PresentModes);

    VkAttachmentDescription depthAttachment = new() {
      format = FindDepthFormat(),
      samples = VkSampleCountFlags.Count1,
      loadOp = VkAttachmentLoadOp.Clear,
      storeOp = VkAttachmentStoreOp.Store,
      stencilLoadOp = VkAttachmentLoadOp.DontCare,
      stencilStoreOp = VkAttachmentStoreOp.DontCare,
      initialLayout = VkImageLayout.Undefined,
      finalLayout = VkImageLayout.DepthStencilReadOnlyOptimal
    };

    VkAttachmentDescription colorAttachment = new() {
      format = surfaceFormat.format,
      samples = VkSampleCountFlags.Count1,
      loadOp = VkAttachmentLoadOp.Clear,
      storeOp = VkAttachmentStoreOp.Store,
      stencilStoreOp = VkAttachmentStoreOp.DontCare,
      stencilLoadOp = VkAttachmentLoadOp.DontCare,
      initialLayout = VkImageLayout.Undefined,
      finalLayout = VkImageLayout.PresentSrcKHR,
    };

    VkAttachmentReference depthAttachmentRef = new() {
      attachment = 1,
      layout = VkImageLayout.DepthStencilAttachmentOptimal
    };

    VkAttachmentReference colorAttachmentRef = new() {
      attachment = 0,
      layout = VkImageLayout.ColorAttachmentOptimal,
    };

    VkAttachmentReference inputAttachmentRef = new() {
      attachment = 1,
      layout = VK_IMAGE_LAYOUT_DEPTH_STENCIL_READ_ONLY_OPTIMAL,
    };

    VkSubpassDescription* subpassDescription = stackalloc VkSubpassDescription[2];
    subpassDescription[0] = new() {
      pipelineBindPoint = VkPipelineBindPoint.Graphics,
      colorAttachmentCount = 1,
      pColorAttachments = &colorAttachmentRef,
      pDepthStencilAttachment = null,
    };

    // subpassDescription[1] = new() {
    //   pipelineBindPoint = VkPipelineBindPoint.Graphics,
    //   colorAttachmentCount = 1,
    //   pColorAttachments = &colorAttachmentRef,
    //   pDepthStencilAttachment = &depthAttachmentRef,
    //   inputAttachmentCount = 1,
    //   pInputAttachments = &inputAttachmentRef
    // };

    // VkSubpassDescription subpass = new() {
    //   pipelineBindPoint = VkPipelineBindPoint.Graphics,
    //   colorAttachmentCount = 1,
    //   pColorAttachments = &colorAttachmentRef,
    //   // inputAttachmentCount = 1,
    //   // pInputAttachments = &inputAttachmentRef,
    //   pDepthStencilAttachment = &depthAttachmentRef
    // };

    VkSubpassDependency* dependencies = stackalloc VkSubpassDependency[2];
    dependencies[0].srcSubpass = VK_SUBPASS_EXTERNAL;
    dependencies[0].dstSubpass = 0;
    dependencies[0].srcStageMask = VK_PIPELINE_STAGE_EARLY_FRAGMENT_TESTS_BIT | VK_PIPELINE_STAGE_LATE_FRAGMENT_TESTS_BIT;
    dependencies[0].dstStageMask = VK_PIPELINE_STAGE_EARLY_FRAGMENT_TESTS_BIT | VK_PIPELINE_STAGE_LATE_FRAGMENT_TESTS_BIT;
    dependencies[0].srcAccessMask = 0;
    dependencies[0].dstAccessMask = VK_ACCESS_DEPTH_STENCIL_ATTACHMENT_WRITE_BIT | VK_ACCESS_DEPTH_STENCIL_ATTACHMENT_READ_BIT;
    dependencies[0].dependencyFlags = 0;

    dependencies[1].srcSubpass = VK_SUBPASS_EXTERNAL;
    dependencies[1].dstSubpass = 0;
    dependencies[1].srcStageMask = VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT;
    dependencies[1].dstStageMask = VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT;
    dependencies[1].srcAccessMask = 0;
    dependencies[1].dstAccessMask = VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT | VK_ACCESS_COLOR_ATTACHMENT_READ_BIT;
    dependencies[1].dependencyFlags = 0;


    VkAttachmentDescription[] attachments = [colorAttachment];
    VkRenderPassCreateInfo renderPassInfo = new() {
      attachmentCount = 1
    };
    fixed (VkAttachmentDescription* ptr = attachments) {
      renderPassInfo.pAttachments = ptr;
    }
    renderPassInfo.subpassCount = 1;
    renderPassInfo.pSubpasses = subpassDescription;
    renderPassInfo.dependencyCount = 2;
    renderPassInfo.pDependencies = dependencies;

    var result = vkCreateRenderPass(_device.LogicalDevice, &renderPassInfo, null, out _postProcessPass);
    if (result != VkResult.Success) throw new Exception("Failed to create render pass!");
  }


  private unsafe void CreateRenderPass_Old() {
    SwapChainSupportDetails swapChainSupport = VkUtils.QuerySwapChainSupport(_device.PhysicalDevice, _device.Surface);
    VkSurfaceFormatKHR surfaceFormat = ChooseSwapSurfaceFormat(swapChainSupport.Formats);
    VkPresentModeKHR presentMode = ChooseSwapPresentMode(swapChainSupport.PresentModes);

    VkAttachmentDescription depthAttachment = new() {
      format = FindDepthFormat(),
      samples = VkSampleCountFlags.Count1,
      loadOp = VkAttachmentLoadOp.Clear,
      storeOp = VkAttachmentStoreOp.Store,
      stencilLoadOp = VkAttachmentLoadOp.DontCare,
      stencilStoreOp = VkAttachmentStoreOp.DontCare,
      initialLayout = VkImageLayout.Undefined,
      finalLayout = VkImageLayout.DepthStencilReadOnlyOptimal
    };

    VkAttachmentDescription colorAttachment = new() {
      format = surfaceFormat.format,
      samples = VkSampleCountFlags.Count1,
      loadOp = VkAttachmentLoadOp.Clear,
      storeOp = VkAttachmentStoreOp.Store,
      stencilStoreOp = VkAttachmentStoreOp.DontCare,
      stencilLoadOp = VkAttachmentLoadOp.DontCare,
      initialLayout = VkImageLayout.Undefined,
      finalLayout = VkImageLayout.PresentSrcKHR,
    };

    VkAttachmentReference depthAttachmentRef = new() {
      attachment = 1,
      layout = VkImageLayout.DepthStencilAttachmentOptimal
    };

    VkAttachmentReference colorAttachmentRef = new() {
      attachment = 0,
      layout = VkImageLayout.ColorAttachmentOptimal,
    };

    VkAttachmentReference inputAttachmentRef = new() {
      attachment = 1,
      layout = VK_IMAGE_LAYOUT_DEPTH_STENCIL_READ_ONLY_OPTIMAL,
    };

    VkSubpassDescription* subpassDescription = stackalloc VkSubpassDescription[2];
    subpassDescription[0] = new() {
      pipelineBindPoint = VkPipelineBindPoint.Graphics,
      colorAttachmentCount = 1,
      pColorAttachments = &colorAttachmentRef,
      pDepthStencilAttachment = &depthAttachmentRef,
    };

    subpassDescription[1] = new() {
      pipelineBindPoint = VkPipelineBindPoint.Graphics,
      colorAttachmentCount = 1,
      pColorAttachments = &colorAttachmentRef,
      pDepthStencilAttachment = &depthAttachmentRef,
      inputAttachmentCount = 1,
      pInputAttachments = &inputAttachmentRef
    };

    // VkSubpassDescription subpass = new() {
    //   pipelineBindPoint = VkPipelineBindPoint.Graphics,
    //   colorAttachmentCount = 1,
    //   pColorAttachments = &colorAttachmentRef,
    //   // inputAttachmentCount = 1,
    //   // pInputAttachments = &inputAttachmentRef,
    //   pDepthStencilAttachment = &depthAttachmentRef
    // };

    VkSubpassDependency* dependencies = stackalloc VkSubpassDependency[2];
    dependencies[0].srcSubpass = VK_SUBPASS_EXTERNAL;
    dependencies[0].dstSubpass = 0;
    dependencies[0].srcStageMask = VK_PIPELINE_STAGE_EARLY_FRAGMENT_TESTS_BIT | VK_PIPELINE_STAGE_LATE_FRAGMENT_TESTS_BIT;
    dependencies[0].dstStageMask = VK_PIPELINE_STAGE_EARLY_FRAGMENT_TESTS_BIT | VK_PIPELINE_STAGE_LATE_FRAGMENT_TESTS_BIT;
    dependencies[0].srcAccessMask = 0;
    dependencies[0].dstAccessMask = VK_ACCESS_DEPTH_STENCIL_ATTACHMENT_WRITE_BIT | VK_ACCESS_DEPTH_STENCIL_ATTACHMENT_READ_BIT;
    dependencies[0].dependencyFlags = 0;

    dependencies[1].srcSubpass = VK_SUBPASS_EXTERNAL;
    dependencies[1].dstSubpass = 0;
    dependencies[1].srcStageMask = VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT;
    dependencies[1].dstStageMask = VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT;
    dependencies[1].srcAccessMask = 0;
    dependencies[1].dstAccessMask = VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT | VK_ACCESS_COLOR_ATTACHMENT_READ_BIT;
    dependencies[1].dependencyFlags = 0;


    VkAttachmentDescription[] attachments = [colorAttachment, depthAttachment];
    VkRenderPassCreateInfo renderPassInfo = new() {
      attachmentCount = 2
    };
    fixed (VkAttachmentDescription* ptr = attachments) {
      renderPassInfo.pAttachments = ptr;
    }
    renderPassInfo.subpassCount = 2;
    renderPassInfo.pSubpasses = subpassDescription;
    renderPassInfo.dependencyCount = 2;
    renderPassInfo.pDependencies = dependencies;

    var result = vkCreateRenderPass(_device.LogicalDevice, &renderPassInfo, null, out _renderPass);
    if (result != VkResult.Success) throw new Exception("Failed to create render pass!");
  }

  private unsafe void CreateColorResources() {
    ReadOnlySpan<VkImage> swapChainImages = vkGetSwapchainImagesKHR(_device.LogicalDevice, _handle);
    SwapChainSupportDetails swapChainSupport = VkUtils.QuerySwapChainSupport(_device.PhysicalDevice, _device.Surface);
    VkSurfaceFormatKHR surfaceFormat = ChooseSwapSurfaceFormat(swapChainSupport.Formats);

    _colorImages = new VkImage[swapChainImages.Length];
    _colorImageMemories = new VkDeviceMemory[swapChainImages.Length];
    _colorImageViews = new VkImageView[swapChainImages.Length];

    for (int i = 0; i < _colorImages.Length; i++) {
      CreateAttachment(
        surfaceFormat.format,
        VK_IMAGE_USAGE_COLOR_ATTACHMENT_BIT | VK_IMAGE_USAGE_SAMPLED_BIT,
        out _colorImages[i],
        out _colorImageMemories[i],
        out _colorImageViews[i],
        out _colorImageFormat
      );
    }
  }

  private unsafe void CreateDepthResources() {
    var depthFormat = FindDepthFormat();
    _swapchainDepthFormat = depthFormat;

    ReadOnlySpan<VkImage> swapChainImages = vkGetSwapchainImagesKHR(_device.LogicalDevice, _handle);

    _depthImages = new VkImage[swapChainImages.Length];
    _depthImagesMemories = new VkDeviceMemory[swapChainImages.Length];
    _depthImageViews = new VkImageView[swapChainImages.Length];

    for (int i = 0; i < _depthImages.Length; i++) {
      CreateAttachment(
        depthFormat,
        VK_IMAGE_USAGE_DEPTH_STENCIL_ATTACHMENT_BIT | VK_IMAGE_USAGE_SAMPLED_BIT,
        out _depthImages[i],
        out _depthImagesMemories[i],
        out _depthImageViews[i],
        out _swapchainDepthFormat
      );
    }
  }

  private unsafe void CreateDepthResources_Old() {
    var depthFormat = FindDepthFormat();
    _swapchainDepthFormat = depthFormat;

    ReadOnlySpan<VkImage> swapChainImages = vkGetSwapchainImagesKHR(_device.LogicalDevice, _handle);

    _depthImages = new VkImage[swapChainImages.Length];
    _depthImagesMemories = new VkDeviceMemory[swapChainImages.Length];
    _depthImageViews = new VkImageView[swapChainImages.Length];

    for (int i = 0; i < _depthImages.Length; i++) {
      VkImageCreateInfo imageInfo = new() {
        imageType = VkImageType.Image2D
      };
      imageInfo.extent.width = _swapchainExtent.width;
      imageInfo.extent.height = _swapchainExtent.height;
      imageInfo.extent.depth = 1;
      imageInfo.mipLevels = 1;
      imageInfo.arrayLayers = 1;
      imageInfo.format = depthFormat;
      imageInfo.tiling = VkImageTiling.Optimal;
      imageInfo.initialLayout = VkImageLayout.Undefined;
      imageInfo.usage = VkImageUsageFlags.DepthStencilAttachment | VkImageUsageFlags.InputAttachment | VkImageUsageFlags.Sampled;
      imageInfo.samples = VkSampleCountFlags.Count1;
      imageInfo.sharingMode = VkSharingMode.Exclusive;
      imageInfo.flags = 0;

      _device.CreateImageWithInfo(imageInfo, VkMemoryPropertyFlags.DeviceLocal, out _depthImages[i], out _depthImagesMemories[i]);

      VkImageViewCreateInfo viewInfo = new() {
        image = _depthImages[i],
        viewType = VkImageViewType.Image2D,
        format = depthFormat
      };
      viewInfo.subresourceRange.aspectMask = VkImageAspectFlags.Depth;
      viewInfo.subresourceRange.baseMipLevel = 0;
      viewInfo.subresourceRange.levelCount = 1;
      viewInfo.subresourceRange.baseArrayLayer = 0;
      viewInfo.subresourceRange.layerCount = 1;

      vkCreateImageView(_device.LogicalDevice, &viewInfo, null, out _depthImageViews[i]).CheckResult();
    }
  }

  private unsafe void CreateAttachment(
    VkFormat format,
    VkImageUsageFlags usage,
    out VkImage image,
    out VkDeviceMemory imageMemory,
    out VkImageView imageView,
    out VkFormat imageFormat
  ) {
    VkImageAspectFlags aspectMask = 0;
    // VkImageLayout imageLayout = new();

    imageFormat = format;

    if ((usage & VK_IMAGE_USAGE_COLOR_ATTACHMENT_BIT) != 0) {
      aspectMask = VK_IMAGE_ASPECT_COLOR_BIT;
      // imageLayout = VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL;
    }
    if ((usage & VK_IMAGE_USAGE_DEPTH_STENCIL_ATTACHMENT_BIT) != 0) {
      aspectMask = VK_IMAGE_ASPECT_DEPTH_BIT;
      // imageLayout = VK_IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL;
    }

    VkImageCreateInfo imageCreateInfo = new() {
      imageType = VK_IMAGE_TYPE_2D,
      format = format,
      extent = new() {
        width = _swapchainExtent.width,
        height = _swapchainExtent.height,
        depth = 1
      },
      mipLevels = 1,
      arrayLayers = 1,
      samples = VK_SAMPLE_COUNT_1_BIT,
      tiling = VK_IMAGE_TILING_OPTIMAL,
      usage = usage | VK_IMAGE_USAGE_INPUT_ATTACHMENT_BIT,
      initialLayout = VK_IMAGE_LAYOUT_UNDEFINED,
    };

    _device.CreateImageWithInfo(imageCreateInfo, VkMemoryPropertyFlags.DeviceLocal, out image, out imageMemory);

    VkImageViewCreateInfo imageViewCreateInfo = new() {
      viewType = VK_IMAGE_VIEW_TYPE_2D,
      format = format,
      subresourceRange = new() {
        aspectMask = aspectMask,
        baseMipLevel = 0,
        levelCount = 1,
        baseArrayLayer = 0,
        layerCount = 1,
      },
      image = image
    };

    vkCreateImageView(_device.LogicalDevice, &imageViewCreateInfo, null, out imageView).CheckResult();
  }

  private unsafe void CreateFramebuffers() {
    ReadOnlySpan<VkImage> swapChainImages = vkGetSwapchainImagesKHR(_device.LogicalDevice, _handle);
    _swapchainFramebuffers = new VkFramebuffer[swapChainImages.Length];

    for (int i = 0; i < swapChainImages.Length; i++) {
      VkImageView[] attachmetns = [_swapChainImageViews[i], _colorImageViews[i], _depthImageViews[i]];
      fixed (VkImageView* ptr = attachmetns) {
        VkFramebufferCreateInfo framebufferInfo = new() {
          renderPass = _renderPass,
          attachmentCount = (uint)attachmetns.Length,
          pAttachments = ptr,
          width = _swapchainExtent.width,
          height = _swapchainExtent.height,
          layers = 1
        };

        vkCreateFramebuffer(_device.LogicalDevice, &framebufferInfo, null, out _swapchainFramebuffers[i]).CheckResult();
      }
    }

    _postProcessFramebuffers = new VkFramebuffer[swapChainImages.Length];
    for (int i = 0; i < swapChainImages.Length; i++) {
      // VkImageView[] attachmetns = [_swapChainImageViews[i], _colorImageViews[i], _depthImageViews[i]];
      VkImageView[] attachmetns = [_swapChainImageViews[i]];
      fixed (VkImageView* ptr2 = attachmetns) {
        VkFramebufferCreateInfo framebufferCreateInfo = new() {
          renderPass = _postProcessPass,
          attachmentCount = (uint)attachmetns.Length,
          pAttachments = ptr2,
          width = _swapchainExtent.width,
          height = _swapchainExtent.height,
          layers = 1
        };

        vkCreateFramebuffer(_device.LogicalDevice, &framebufferCreateInfo, null, out _postProcessFramebuffers[i]).CheckResult();
      }
    }
  }

  public unsafe void UpdateDescriptors(int index) {
    VkDescriptorImageInfo* descriptorImageInfo = stackalloc VkDescriptorImageInfo[2];
    descriptorImageInfo[0] = new() {
      imageLayout = VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL,
      imageView = _colorImageViews[index],
      sampler = VkSampler.Null
    };
    descriptorImageInfo[1] = new() {
      imageLayout = VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL,
      imageView = _depthImageViews[index],
      sampler = VkSampler.Null
    };

    VkWriteDescriptorSet* writeDescriptorSets = stackalloc VkWriteDescriptorSet[2];
    writeDescriptorSets[0] = new() {
      dstSet = _imageDescriptors[index],
      descriptorType = VK_DESCRIPTOR_TYPE_INPUT_ATTACHMENT,
      descriptorCount = 1,
      dstBinding = 0,
      pImageInfo = &descriptorImageInfo[0]
    };
    writeDescriptorSets[1] = new() {
      dstSet = _imageDescriptors[index],
      descriptorType = VK_DESCRIPTOR_TYPE_INPUT_ATTACHMENT,
      descriptorCount = 1,
      dstBinding = 1,
      pImageInfo = &descriptorImageInfo[1]
    };

    vkUpdateDescriptorSets(_device.LogicalDevice, 2, writeDescriptorSets, 0, null);
  }

  public unsafe void UpdatePostProcessDescriptors(int index) {
    VkDescriptorImageInfo* descriptorImageInfo = stackalloc VkDescriptorImageInfo[2];
    descriptorImageInfo[0] = new() {
      imageLayout = VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL,
      imageView = _colorImageViews[index],
      sampler = _colorSampler // Sampler for the color image
    };
    descriptorImageInfo[1] = new() {
      imageLayout = VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL,
      imageView = _depthImageViews[index],
      sampler = _depthSampler // Sampler for the depth image
    };

    VkWriteDescriptorSet* writeDescriptorSets = stackalloc VkWriteDescriptorSet[2];
    writeDescriptorSets[0] = new() {
      dstSet = _postProcessDescriptors[index],
      descriptorType = VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER,
      descriptorCount = 1,
      dstBinding = 0,
      pImageInfo = &descriptorImageInfo[0]
    };
    writeDescriptorSets[1] = new() {
      dstSet = _postProcessDescriptors[index],
      descriptorType = VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER,
      descriptorCount = 1,
      dstBinding = 1,
      pImageInfo = &descriptorImageInfo[1]
    };

    vkUpdateDescriptorSets(_device.LogicalDevice, 2, writeDescriptorSets, 0, null);
  }

  private unsafe void CreateDescriptors(int index) {
    var setLayout = _inputAttachmentsLayout.GetDescriptorSetLayout();

    VkDescriptorSet descriptorSet = new();
    var allocInfo = new VkDescriptorSetAllocateInfo {
      descriptorPool = _descriptorPool.GetVkDescriptorPool(),
      descriptorSetCount = 1,
      pSetLayouts = &setLayout
    };
    vkAllocateDescriptorSets(_device.LogicalDevice, &allocInfo, &descriptorSet);

    VkDescriptorImageInfo* descriptorImageInfo = stackalloc VkDescriptorImageInfo[2];
    descriptorImageInfo[0] = new() {
      imageLayout = VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL,
      imageView = _colorImageViews[_currentFrame],
      sampler = VkSampler.Null
    };
    descriptorImageInfo[1] = new() {
      imageLayout = VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL,
      imageView = _depthImageViews[_currentFrame],
      sampler = VkSampler.Null
    };

    VkWriteDescriptorSet* writeDescriptorSets = stackalloc VkWriteDescriptorSet[2];
    writeDescriptorSets[0] = new() {
      dstSet = descriptorSet,
      descriptorType = VK_DESCRIPTOR_TYPE_INPUT_ATTACHMENT,
      descriptorCount = 1,
      dstBinding = 0,
      pImageInfo = &descriptorImageInfo[0]
    };
    writeDescriptorSets[1] = new() {
      dstSet = descriptorSet,
      descriptorType = VK_DESCRIPTOR_TYPE_INPUT_ATTACHMENT,
      descriptorCount = 1,
      dstBinding = 1,
      pImageInfo = &descriptorImageInfo[1]
    };

    vkUpdateDescriptorSets(_device.LogicalDevice, 2, writeDescriptorSets, 0, null);
    _imageDescriptors[index] = descriptorSet;
  }

  private unsafe void CreatePostProcessDescriptors(int index) {
    var setLayout = _postProcessLayout.GetDescriptorSetLayout();

    VkDescriptorSet descriptorSet = new();
    var allocInfo = new VkDescriptorSetAllocateInfo {
      descriptorPool = _descriptorPool.GetVkDescriptorPool(),
      descriptorSetCount = 1,
      pSetLayouts = &setLayout
    };
    vkAllocateDescriptorSets(_device.LogicalDevice, &allocInfo, &descriptorSet);

    VkDescriptorImageInfo* descriptorImageInfo = stackalloc VkDescriptorImageInfo[2];
    descriptorImageInfo[0] = new() {
      imageLayout = VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL,
      imageView = _colorImageViews[_currentFrame],
      sampler = _colorSampler
    };
    descriptorImageInfo[1] = new() {
      imageLayout = VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL,
      imageView = _depthImageViews[_currentFrame],
      sampler = _depthSampler
    };

    VkWriteDescriptorSet* writeDescriptorSets = stackalloc VkWriteDescriptorSet[2];
    writeDescriptorSets[0] = new() {
      dstSet = descriptorSet,
      descriptorType = VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER,
      descriptorCount = 1,
      dstBinding = 0,
      pImageInfo = &descriptorImageInfo[0]
    };
    writeDescriptorSets[1] = new() {
      dstSet = descriptorSet,
      descriptorType = VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER,
      descriptorCount = 1,
      dstBinding = 1,
      pImageInfo = &descriptorImageInfo[1]
    };

    vkUpdateDescriptorSets(_device.LogicalDevice, 2, writeDescriptorSets, 0, null);
    _postProcessDescriptors[index] = descriptorSet;
  }

  private unsafe void CreateDescriptors() {
    _inputAttachmentsLayout = new VulkanDescriptorSetLayout.Builder(_device)
      .AddBinding(0, DescriptorType.InputAttachment, ShaderStageFlags.Fragment)
      .AddBinding(1, DescriptorType.InputAttachment, ShaderStageFlags.Fragment)
      .Build();

    _postProcessLayout = new VulkanDescriptorSetLayout.Builder(_device)
      .AddBinding(0, DescriptorType.CombinedImageSampler, ShaderStageFlags.AllGraphics)
      .AddBinding(1, DescriptorType.CombinedImageSampler, ShaderStageFlags.AllGraphics)
      .Build();

    _descriptorPool = new VulkanDescriptorPool.Builder(_device)
      .SetMaxSets(100)
      .AddPoolSize(DescriptorType.InputAttachment, 10)
      .AddPoolSize(DescriptorType.CombinedImageSampler, 20)
      .Build();

    _imageDescriptors = new VkDescriptorSet[_colorImageViews.Length];
    _postProcessDescriptors = new VkDescriptorSet[_colorImageViews.Length];

    for (int i = 0; i < _imageDescriptors.Length; i++) {
      CreateDescriptors(i);
    }
    for (int i = 0; i < _postProcessDescriptors.Length; i++) {
      CreatePostProcessDescriptors(i);
    }
  }

  private unsafe void CreateSyncObjects() {
    ReadOnlySpan<VkImage> swapChainImages = vkGetSwapchainImagesKHR(_device.LogicalDevice, _handle);
    _imageAvailableSemaphores = MemoryUtils.AllocateMemory<VkSemaphore>(MAX_FRAMES_IN_FLIGHT);
    _renderFinishedSemaphores = MemoryUtils.AllocateMemory<VkSemaphore>(MAX_FRAMES_IN_FLIGHT);
    _inFlightFences = MemoryUtils.AllocateMemory<VkFence>(MAX_FRAMES_IN_FLIGHT);
    _imagesInFlight = MemoryUtils.AllocateMemory<VkFence>(swapChainImages.Length);
    for (int i = 0; i < swapChainImages.Length; i++) {
      _imagesInFlight[i] = VkFence.Null;
    }

    VkSemaphoreCreateInfo semaphoreInfo = new();

    VkFenceCreateInfo fenceInfo = new() {
      flags = VkFenceCreateFlags.Signaled
    };

    for (int i = 0; i < MAX_FRAMES_IN_FLIGHT; i++) {
      vkCreateSemaphore(_device.LogicalDevice, &semaphoreInfo, null, out _imageAvailableSemaphores[i]).CheckResult();
      vkCreateSemaphore(_device.LogicalDevice, &semaphoreInfo, null, out _renderFinishedSemaphores[i]).CheckResult();
      // vkCreateFence(_device.LogicalDevice, &fenceInfo, null, out _inFlightFences[i]).CheckResult();
      vkCreateFence(_device.LogicalDevice, &fenceInfo, null, &_inFlightFences[i]).CheckResult();
    }
  }

  private unsafe void CreateSamplers() {
    CreateSampler(out _colorSampler);
    CreateSampler(out _depthSampler);
  }

  private unsafe void CreateSampler(out VkSampler sampler) {
    VkPhysicalDeviceProperties properties = new();
    vkGetPhysicalDeviceProperties(_device.PhysicalDevice, &properties);

    VkSamplerCreateInfo samplerInfo = new();
    samplerInfo.magFilter = VkFilter.Nearest;
    samplerInfo.minFilter = VkFilter.Nearest;
    samplerInfo.addressModeU = VkSamplerAddressMode.Repeat;
    samplerInfo.addressModeV = VkSamplerAddressMode.Repeat;
    samplerInfo.addressModeW = VkSamplerAddressMode.Repeat;
    samplerInfo.anisotropyEnable = true;
    samplerInfo.maxAnisotropy = properties.limits.maxSamplerAnisotropy;
    samplerInfo.borderColor = VkBorderColor.IntOpaqueBlack;
    samplerInfo.unnormalizedCoordinates = false;
    samplerInfo.compareEnable = false;
    samplerInfo.compareOp = VkCompareOp.Always;
    samplerInfo.mipmapMode = VkSamplerMipmapMode.Nearest;

    vkCreateSampler(_device.LogicalDevice, &samplerInfo, null, out sampler).CheckResult();
  }

  public unsafe VkResult AcquireNextImage(out uint imageIndex) {
    // vkWaitForFences(_device.LogicalDevice, MAX_FRAMES_IN_FLIGHT, _inFlightFences, true, UInt64.MaxValue);

    VkResult result = vkAcquireNextImageKHR(
      _device.LogicalDevice,
      _handle,
      UInt64.MaxValue,
      _imageAvailableSemaphores[_currentFrame],
      VkFence.Null,
      out imageIndex
    );
    _imageIndex = imageIndex;
    // vkResetFences(_device.LogicalDevice, _imagesInFlight[imageIndex]);

    return result;
  }
  public unsafe VkResult SubmitCommandBuffers(VkCommandBuffer* buffers, uint imageIndex) {
    Application.Mutex.WaitOne();

    // if (_imagesInFlight[imageIndex] != VkFence.Null) {
    //   vkWaitForFences(_device.LogicalDevice, 1, &_inFlightFences[_currentFrame], true, UInt64.MaxValue);
    // }
    // _imagesInFlight[imageIndex] = _inFlightFences[_currentFrame];

    VkSubmitInfo submitInfo = new();

    VkPipelineStageFlags* waitStages = stackalloc VkPipelineStageFlags[1];
    waitStages[0] = VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT;

    submitInfo.waitSemaphoreCount = 1;
    submitInfo.pWaitSemaphores = &_imageAvailableSemaphores[_currentFrame];
    submitInfo.pWaitDstStageMask = waitStages;

    submitInfo.commandBufferCount = 1;
    submitInfo.pCommandBuffers = buffers;

    VkSwapchainKHR* swapchains = stackalloc VkSwapchainKHR[1];
    swapchains[0] = _handle;

    // VkSemaphore* signalSemaphores = stackalloc VkSemaphore[1];
    // signalSemaphores[0] = _renderFinishedSemaphores[_currentFrame];

    submitInfo.signalSemaphoreCount = 1;
    submitInfo.pSignalSemaphores = &_renderFinishedSemaphores[_currentFrame];
    submitInfo.pNext = null;

    vkWaitForFences(_device.LogicalDevice, MAX_FRAMES_IN_FLIGHT, _inFlightFences, true, UInt64.MaxValue);
    vkResetFences(_device.LogicalDevice, _inFlightFences[_currentFrame]);
    // _device.SubmitQueue(1, &submitInfo, _inFlightFences[_currentFrame]);
    var queueResult = vkQueueSubmit(_device.GraphicsQueue, 1, &submitInfo, _inFlightFences[_currentFrame]);
    if (queueResult == VkResult.ErrorDeviceLost) {
      throw new VkException($"Device Lost! - {queueResult}");
    }

    VkPresentInfoKHR presentInfo = new() {
      waitSemaphoreCount = 1,
      pWaitSemaphores = &_renderFinishedSemaphores[_currentFrame],
      swapchainCount = 1,
      pSwapchains = swapchains,
      pImageIndices = &imageIndex,
      pNext = null,
    };

    var result = vkQueuePresentKHR(_device.GraphicsQueue, &presentInfo);
    _previousFrame = _currentFrame;
    _currentFrame = (_currentFrame + 1) % MAX_FRAMES_IN_FLIGHT;

    Application.Mutex.ReleaseMutex();
    return result;
  }

  private VkFormat FindDepthFormat() {
    var items = new List<VkFormat> {
      VkFormat.D32Sfloat,
      VkFormat.D32SfloatS8Uint,
      VkFormat.D24UnormS8Uint
    };
    return _device.FindSupportedFormat(items, VkImageTiling.Optimal, VkFormatFeatureFlags.DepthStencilAttachment);
  }

  private VkSurfaceFormatKHR ChooseSwapSurfaceFormat(ReadOnlySpan<VkSurfaceFormatKHR> availableFormats) {
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
      if (availableFormat.format == VkFormat.R8G8B8A8Srgb) {
        return availableFormat;
      }
    }

    return availableFormats[0];
  }

  private static VkPresentModeKHR ChooseSwapPresentMode(ReadOnlySpan<VkPresentModeKHR> availablePresentModes) {
    if (Application.Instance.VSync) {
      var targetMode = VkPresentModeKHR.Fifo;
      Logger.Info($"[SWAPCHAIN] Present Mode is set to: {targetMode}");
      return targetMode;
    }

    foreach (VkPresentModeKHR availablePresentMode in availablePresentModes) {
      // render mode
      if (
        availablePresentMode == VkPresentModeKHR.Mailbox ||
        availablePresentMode == VkPresentModeKHR.Immediate
      ) {
        Logger.Info($"[SWAPCHAIN] Present Mode is set to: {availablePresentMode}");
        return availablePresentMode;
      }
    }

    Logger.Info($"[SWAPCHAIN] Present Mode is set to: {VkPresentModeKHR.Fifo}");
    return VkPresentModeKHR.Fifo;
  }

  private VkExtent2D ChooseSwapExtent(VkSurfaceCapabilitiesKHR capabilities) {
    if (capabilities.currentExtent.width > 0) {
      return capabilities.currentExtent;
    } else {
      VkExtent2D actualExtent = Extent2D;

      actualExtent = new VkExtent2D(
        System.Math.Max(capabilities.minImageExtent.width, System.Math.Min(capabilities.maxImageExtent.width, actualExtent.width)),
        System.Math.Max(capabilities.minImageExtent.height, System.Math.Min(capabilities.maxImageExtent.height, actualExtent.height))
      );

      return actualExtent;
    }
  }

  public unsafe void Dispose() {
    _inputAttachmentsLayout?.Dispose();
    _postProcessLayout?.Dispose();
    _descriptorPool?.Dispose();

    for (int i = 0; i < MAX_FRAMES_IN_FLIGHT; i++) {
      vkDestroySemaphore(_device.LogicalDevice, _imageAvailableSemaphores[i], null);
      vkDestroySemaphore(_device.LogicalDevice, _renderFinishedSemaphores[i], null);
    }

    for (int i = 0; i < _swapChainImageViews.Length; i++) {
      vkDestroyImageView(_device.LogicalDevice, _swapChainImageViews[i]);
    }

    for (int i = 0; i < _depthImages.Length; i++) {
      vkDestroyImage(_device.LogicalDevice, _depthImages[i]);
    }
    for (int i = 0; i < _depthImagesMemories.Length; i++) {
      vkFreeMemory(_device.LogicalDevice, _depthImagesMemories[i]);
    }
    for (int i = 0; i < _depthImageViews.Length; i++) {
      vkDestroyImageView(_device.LogicalDevice, _depthImageViews[i]);
    }

    for (int i = 0; i < _colorImages.Length; i++) {
      vkDestroyImage(_device.LogicalDevice, _colorImages[i]);
    }
    for (int i = 0; i < _colorImageViews.Length; i++) {
      vkDestroyImageView(_device.LogicalDevice, _colorImageViews[i]);
    }
    for (int i = 0; i < _colorImageMemories.Length; i++) {
      vkFreeMemory(_device.LogicalDevice, _colorImageMemories[i]);
    }

    for (int i = 0; i < _normalImages.Length; i++) {
      vkDestroyImage(_device.LogicalDevice, _normalImages[i]);
    }
    for (int i = 0; i < _normalImagesMemories.Length; i++) {
      vkFreeMemory(_device.LogicalDevice, _normalImagesMemories[i]);
    }
    for (int i = 0; i < _normalImageViews.Length; i++) {
      vkDestroyImageView(_device.LogicalDevice, _normalImageViews[i]);
    }

    for (int i = 0; i < _swapchainFramebuffers.Length; i++) {
      vkDestroyFramebuffer(_device.LogicalDevice, _swapchainFramebuffers[i]);
    }
    for (int i = 0; i < _postProcessFramebuffers.Length; i++) {
      vkDestroyFramebuffer(_device.LogicalDevice, _postProcessFramebuffers[i]);
    }

    for (int i = 0; i < MAX_FRAMES_IN_FLIGHT; i++) {
      vkDestroySemaphore(_device.LogicalDevice, _imageAvailableSemaphores[i]);
      vkDestroySemaphore(_device.LogicalDevice, _renderFinishedSemaphores[i]);
      if (_inFlightFences[i] != VkFence.Null)
        vkDestroyFence(_device.LogicalDevice, _inFlightFences[i]);
    }

    MemoryUtils.FreeIntPtr<VkSemaphore>((nint)_imageAvailableSemaphores);
    MemoryUtils.FreeIntPtr<VkSemaphore>((nint)_renderFinishedSemaphores);

    MemoryUtils.FreeIntPtr<VkFence>((nint)_imagesInFlight);
    MemoryUtils.FreeIntPtr<VkFence>((nint)_inFlightFences);

    vkDestroyRenderPass(_device.LogicalDevice, _renderPass);
    vkDestroyRenderPass(_device.LogicalDevice, _postProcessPass);
    vkDestroySwapchainKHR(_device.LogicalDevice, _handle);

    vkDestroySampler(_device.LogicalDevice, _colorSampler);
    vkDestroySampler(_device.LogicalDevice, _depthSampler);

    for (int i = 0; i < _swapchainImages.Length; i++) {
      // vkDestroyImage(_device.LogicalDevice, _swapchainImages[i]);
    }
  }

  private uint GetImageCount() {
    SwapChainSupportDetails swapChainSupport = VkUtils.QuerySwapChainSupport(_device.PhysicalDevice, _device.Surface);
    uint imageCount = swapChainSupport.Capabilities.minImageCount + 1;
    if (swapChainSupport.Capabilities.maxImageCount > 0 &&
        imageCount > swapChainSupport.Capabilities.maxImageCount) {
      imageCount = swapChainSupport.Capabilities.maxImageCount;
    }
    return imageCount;
  }

  public VkFramebuffer GetFramebuffer(int index) {
    return _swapchainFramebuffers[index];
  }

  public VkFramebuffer GetPostProcessFramebuffer(int index) {
    return _postProcessFramebuffers[index];
  }

  public VkSwapchainKHR Handle => _handle;
  public VkExtent2D Extent2D { get; }
  public unsafe VkFence* CurrentFence => &_inFlightFences[_currentFrame];
  public unsafe ulong CurrentSemaphore => _renderFinishedSemaphores[_currentFrame].Handle;
  public VkRenderPass RenderPass => _renderPass;
  public VkRenderPass PostProcessPass => _postProcessPass;
  public VkImageView CurrentImageDepthView => _depthImageViews[_currentFrame];
  public VkImageView GetImageDepthView(uint idx) => _depthImageViews[idx];
  public VkImage CurrentImageDepth => _depthImages[_currentFrame];
  public VkImage GetImageDepth(uint idx) => _depthImages[idx];
  public VkImage CurrentImageColor => _colorImages[_currentFrame];
  public VkImage GetImageColor(uint idx) => _colorImages[idx];
  public VkImageView CurrentImageColorView => _colorImageViews[_currentFrame];
  public VkImageView GetImageColorView(uint idx) => _colorImageViews[idx];
  public VkDescriptorSet ImageDescriptor => _imageDescriptors[_currentFrame];
  public VkDescriptorSet PostProcessDecriptor => _postProcessDescriptors[_currentFrame];
  public VkDescriptorSet PreviousPostProcessDescriptor => _postProcessDescriptors[_previousFrame];
  public VulkanDescriptorSetLayout InputAttachmentLayout => _inputAttachmentsLayout;
  public uint ImageCount => GetImageCount();
  public int GetMaxFramesInFlight() => MAX_FRAMES_IN_FLIGHT;
  public float ExtentAspectRatio() {
    return _swapchainExtent.width / (float)_swapchainExtent.height;
  }
  public int PreviousFrame => _previousFrame;
  public int CurrentFrame => _currentFrame;

  public static VkFormat SurfaceFormat = VkFormat.Undefined;
  public static VkFormat DepthFormat = VkFormat.Undefined;
}