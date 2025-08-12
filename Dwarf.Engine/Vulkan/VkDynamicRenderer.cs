using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Dwarf.AbstractionLayer;
using Dwarf.Extensions.Logging;
using Dwarf.Math;
using Dwarf.Rendering;
using Dwarf.Utils;
using Dwarf.Vulkan;
using Dwarf.Windowing;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Vulkan;

public unsafe class VkDynamicRenderer : IRenderer {
  private readonly IWindow _window = null!;
  private readonly VulkanDevice _device;
  private readonly Application _application;
  private VulkanDynamicSwapchain _swapchain = null!;

  private VkCommandBuffer[] _commandBuffers = [];
  private VulkanDescriptorPool _descriptorPool = null!;
  private uint _imageIndex = 0;

  private VkFence[] _waitFences = [];

  public delegate void RenderDelegate();

  public DwarfFormat DepthFormat { get; private set; }
  public VkDescriptorSet[] ImageDescriptors { get; private set; } = [];
  public VkDescriptorSet[] ColorDescriptors { get; private set; } = [];
  public VkDescriptorSet[] DepthDescriptors { get; private set; } = [];
  public VkDescriptorSet CurrentColor => ImageDescriptors[_imageIndex];
  public VkDescriptorSet CurrentDepth => DepthDescriptors[_imageIndex];
  private VulkanDescriptorSetLayout _postProcessLayout = null!;
  public VkSampler DepthSampler { get; private set; }
  public VkSampler ImageSampler { get; private set; }

  internal class AttachmentImage {
    internal VkImage Image;
    internal VkImageView ImageView;
    internal VkDeviceMemory ImageMemory;
  }
  private AttachmentImage[] _depthStencil = [];
  // private AttachmentImage[] _colorImage = [];
  private AttachmentImage[] _sceneColor = [];

  internal class Semaphores {
    internal VkSemaphore PresentComplete;
    internal VkSemaphore RenderComplete;
  }
  private Semaphores[] _semaphores = [];

  public VkDynamicRenderer(Application application) {
    _application = application;
    _window = _application.Window;
    _device = (VulkanDevice)_application.Device;

    CommandList = new VulkanCommandList();

    RecreateSwapchain();

    InitVulkan();
    CreateSamplers();
    CreateDescriptors();
  }

  public void UpdateDescriptors() {
    UpdateColorDescriptor();
    UpdateDepthDescriptor();
  }

  private void UpdateColorDescriptor() {
    // if (ColorDescriptors[_imageIndex].IsNull) return;

    // var copyRegion = new VkImageCopy2 {
    //   srcSubresource = new() {
    //     aspectMask = VK_IMAGE_ASPECT_COLOR_BIT,
    //     layerCount = 1
    //   },
    //   dstSubresource = new() {
    //     aspectMask = VK_IMAGE_ASPECT_COLOR_BIT,
    //     layerCount = 1
    //   },
    //   extent = new(Extent2D.Width, Extent2D.Height, 1)
    // };

    // var imageCopyInfo = new VkCopyImageInfo2 {
    //   srcImage = Swapchain.Images[_imageIndex],
    //   srcImageLayout = VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
    //   dstImage = _colorImage[_imageIndex].Image,
    //   dstImageLayout = VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
    //   regionCount = 1,
    //   pRegions = &copyRegion
    // };

    // vkCmdCopyImage2(CurrentCommandBuffer, &imageCopyInfo);

    // var colorDescriptor = new VkDescriptorImageInfo() {
    //   imageLayout = VkImageLayout.ColorAttachmentOptimal,
    //   imageView = _colorImage[_imageIndex].ImageView,
    //   sampler = ImageSampler
    // };

    // var writeDescriptorSets = new VkWriteDescriptorSet() {
    //   dstSet = ColorDescriptors[_imageIndex],
    //   descriptorType = VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER,
    //   descriptorCount = 1,
    //   dstBinding = 0,
    //   pImageInfo = &colorDescriptor
    // };
    // vkUpdateDescriptorSets(_device.LogicalDevice, 1, &writeDescriptorSets, 0, null);

    // ColorDescriptors[_imageIndex] = descriptorSet;
  }

  private void UpdateDepthDescriptor() {
    // if (DepthDescriptors[_imageIndex].IsNull) return;

    // var depthDescriptor = new VkDescriptorImageInfo() {
    //   imageLayout = VK_IMAGE_LAYOUT_DEPTH_READ_ONLY_OPTIMAL,
    //   imageView = _depthStencil[_imageIndex].ImageView,
    //   sampler = DepthSampler
    // };

    // var writeDescriptorSets = new VkWriteDescriptorSet() {
    //   dstSet = DepthDescriptors[_imageIndex],
    //   descriptorType = VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER,
    //   descriptorCount = 1,
    //   dstBinding = 0,
    //   pImageInfo = &depthDescriptor
    // };
    // vkUpdateDescriptorSets(_device.LogicalDevice, 1, &writeDescriptorSets, 0, null);
  }

  public void UpdateDescriptors_() {
    VkDescriptorImageInfo* descriptorImageInfo = stackalloc VkDescriptorImageInfo[2];
    descriptorImageInfo[0] = new() {
      imageLayout = VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL,
      imageView = Swapchain.ImageViews[_imageIndex],
      sampler = ImageSampler
    };
    descriptorImageInfo[1] = new() {
      imageLayout = VK_IMAGE_LAYOUT_DEPTH_READ_ONLY_OPTIMAL,
      imageView = _depthStencil[_imageIndex].ImageView,
      sampler = DepthSampler
    };

    VkWriteDescriptorSet* writeDescriptorSets = stackalloc VkWriteDescriptorSet[2];
    writeDescriptorSets[0] = new() {
      dstSet = ImageDescriptors[_imageIndex],
      descriptorType = VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER,
      descriptorCount = 1,
      dstBinding = 0,
      pImageInfo = &descriptorImageInfo[0]
    };
    writeDescriptorSets[1] = new() {
      dstSet = ImageDescriptors[_imageIndex],
      descriptorType = VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER,
      descriptorCount = 1,
      dstBinding = 1,
      pImageInfo = &descriptorImageInfo[1]
    };
    vkUpdateDescriptorSets(_device.LogicalDevice, 2, writeDescriptorSets, 0, null);
  }

  private void CreateSamplers() {
    CreateSampler(out var imgSampler, compare: false);
    CreateSampler(out var depthSampler, compare: true);

    ImageSampler = imgSampler;
    DepthSampler = depthSampler;
  }

  private void CreateSampler(out VkSampler sampler, bool compare = false) {
    VkPhysicalDeviceProperties2 properties = new();
    vkGetPhysicalDeviceProperties2(_device.PhysicalDevice, &properties);

    VkSamplerCreateInfo samplerInfo = new();
    samplerInfo.magFilter = VkFilter.Nearest;
    samplerInfo.minFilter = VkFilter.Nearest;
    samplerInfo.addressModeU = VkSamplerAddressMode.ClampToEdge;
    samplerInfo.addressModeV = VkSamplerAddressMode.ClampToEdge;
    samplerInfo.addressModeW = VkSamplerAddressMode.ClampToEdge;
    samplerInfo.anisotropyEnable = false;
    samplerInfo.maxAnisotropy = properties.properties.limits.maxSamplerAnisotropy;
    samplerInfo.borderColor = VkBorderColor.FloatOpaqueWhite;
    samplerInfo.unnormalizedCoordinates = false;
    samplerInfo.compareEnable = compare;
    samplerInfo.compareOp = compare ? VkCompareOp.LessOrEqual : VkCompareOp.Always;
    samplerInfo.mipmapMode = VkSamplerMipmapMode.Nearest;

    vkCreateSampler(_device.LogicalDevice, &samplerInfo, null, out sampler).CheckResult();
  }

  private unsafe void CreateDescriptors() {
    _postProcessLayout = new VulkanDescriptorSetLayout.Builder(_device)
      .AddBinding(0, DescriptorType.CombinedImageSampler, ShaderStageFlags.AllGraphics)
      .AddBinding(1, DescriptorType.CombinedImageSampler, ShaderStageFlags.AllGraphics)
      .Build();

    _descriptorPool = new VulkanDescriptorPool.Builder(_device)
      .SetMaxSets(100)
      .AddPoolSize(DescriptorType.InputAttachment, 100)
      .AddPoolSize(DescriptorType.CombinedImageSampler, 200)
      .SetPoolFlags(DescriptorPoolCreateFlags.None)
      .Build();

    ImageDescriptors = new VkDescriptorSet[Swapchain.ImageViews.Length];
    ColorDescriptors = new VkDescriptorSet[Swapchain.ImageViews.Length];
    DepthDescriptors = new VkDescriptorSet[Swapchain.ImageViews.Length];
    for (int i = 0; i < ImageDescriptors.Length; i++) {
      // CreateImageDescriptor(i);
      // CreateColorDescriptor(i);
      // CreateDepthDescriptor(i);
      CreateSceneDescriptors(i);
    }
  }

  private void CreateSceneDescriptors(int index) {
    var setLayout = _postProcessLayout.GetDescriptorSetLayout();

    VkDescriptorSet descriptorSet = new();
    var allocInfo = new VkDescriptorSetAllocateInfo {
      descriptorPool = _descriptorPool.GetVkDescriptorPool(),
      descriptorSetCount = 1,
      pSetLayouts = &setLayout
    };
    vkAllocateDescriptorSets(_device.LogicalDevice, &allocInfo, &descriptorSet);

    VkDescriptorImageInfo colorInfo = new() {
      imageLayout = VkImageLayout.ShaderReadOnlyOptimal,
      imageView = _sceneColor[index].ImageView,
      sampler = ImageSampler
    };
    VkDescriptorImageInfo depthInfo = new() {
      imageLayout = VkImageLayout.DepthReadOnlyOptimal,
      imageView = _depthStencil[index].ImageView,
      sampler = DepthSampler
    };

    VkWriteDescriptorSet* writeDescriptorSets = stackalloc VkWriteDescriptorSet[2];
    writeDescriptorSets[0] = new() {
      dstSet = descriptorSet,
      descriptorType = VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER,
      descriptorCount = 1,
      dstBinding = 0,
      pImageInfo = &colorInfo
    };
    writeDescriptorSets[1] = new() {
      dstSet = descriptorSet,
      descriptorType = VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER,
      descriptorCount = 1,
      dstBinding = 1,
      pImageInfo = &depthInfo
    };

    vkUpdateDescriptorSets(_device.LogicalDevice, 1, writeDescriptorSets, 0, null);
    ImageDescriptors[index] = descriptorSet;
  }

  private void CreateColorDescriptor(int index) {
    var setLayout = _postProcessLayout.GetDescriptorSetLayout();

    VkDescriptorSet descriptorSet = new();
    var allocInfo = new VkDescriptorSetAllocateInfo {
      descriptorPool = _descriptorPool.GetVkDescriptorPool(),
      descriptorSetCount = 1,
      pSetLayouts = &setLayout
    };
    vkAllocateDescriptorSets(_device.LogicalDevice, &allocInfo, &descriptorSet);

    VkDescriptorImageInfo descriptorImageInfo = new() {
      imageLayout = VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL,
      imageView = Swapchain.ImageViews[index],
      sampler = ImageSampler
    };

    VkWriteDescriptorSet writeDescriptorSets = new() {
      dstSet = descriptorSet,
      descriptorType = VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER,
      descriptorCount = 1,
      dstBinding = 0,
      pImageInfo = &descriptorImageInfo
    };

    vkUpdateDescriptorSets(_device.LogicalDevice, 1, &writeDescriptorSets, 0, null);
    ColorDescriptors[index] = descriptorSet;
  }

  private void CreateDepthDescriptor(int index) {
    var setLayout = _postProcessLayout.GetDescriptorSetLayout();

    VkDescriptorSet descriptorSet = new();
    var allocInfo = new VkDescriptorSetAllocateInfo {
      descriptorPool = _descriptorPool.GetVkDescriptorPool(),
      descriptorSetCount = 1,
      pSetLayouts = &setLayout
    };
    vkAllocateDescriptorSets(_device.LogicalDevice, &allocInfo, &descriptorSet);

    VkDescriptorImageInfo descriptorImageInfo = new() {
      imageLayout = VK_IMAGE_LAYOUT_DEPTH_READ_ONLY_OPTIMAL,
      imageView = _depthStencil[index].ImageView,
      sampler = DepthSampler
    };

    VkWriteDescriptorSet writeDescriptorSets = new() {
      dstSet = descriptorSet,
      descriptorType = VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER,
      descriptorCount = 1,
      dstBinding = 0,
      pImageInfo = &descriptorImageInfo
    };

    vkUpdateDescriptorSets(_device.LogicalDevice, 1, &writeDescriptorSets, 0, null);
    DepthDescriptors[index] = descriptorSet;
  }

  private void CreateImageDescriptor(int index) {
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
      imageLayout = VK_IMAGE_LAYOUT_ATTACHMENT_OPTIMAL,
      imageView = Swapchain.ImageViews[index],
      sampler = ImageSampler
    };
    descriptorImageInfo[1] = new() {
      imageLayout = VK_IMAGE_LAYOUT_ATTACHMENT_OPTIMAL,
      imageView = _depthStencil[index].ImageView,
      sampler = DepthSampler
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
    ImageDescriptors[index] = descriptorSet;
  }
  private void PrepareFrame() {
    fixed (VkFence* fences = _waitFences) {
      vkWaitForFences(_device.LogicalDevice, (uint)Swapchain.Images.Length, fences, true, UInt64.MaxValue);
    }

    var result = _swapchain.AcquireNextImage(_semaphores[Swapchain.CurrentFrame].PresentComplete, out _imageIndex);
    if (result == VkResult.ErrorOutOfDateKHR) {
      RecreateSwapchain();
    }

    if (result != VkResult.Success && result != VkResult.SuboptimalKHR) {
      result.CheckResult();
      RecreateSwapchain();
    }
  }

  private void SubmitFrame() {
    var result = _swapchain.QueuePresent(
      _device.GraphicsQueue,
      _imageIndex,
      _semaphores[Swapchain.CurrentFrame].RenderComplete,
      _waitFences
    );

    if (result == VkResult.ErrorOutOfDateKHR || result == VkResult.SuboptimalKHR || _window.WasWindowResized()) {
      _window.ResetWindowResizedFlag();
      RecreateSwapchain();
      return;
    } else if (result != VkResult.Success) {
      result.CheckResult();
    }

    FrameIndex = (FrameIndex + 1) % Swapchain.Images.Length;
    vkQueueWaitIdle(_device.GraphicsQueue).CheckResult();
  }

  private unsafe void InitVulkan() {
    VkFenceCreateInfo fenceCreateInfo = new() {
      flags = VK_FENCE_CREATE_SIGNALED_BIT
    };
    _waitFences = new VkFence[Swapchain.Images.Length];
    for (int i = 0; i < _waitFences.Length; i++) {
      vkCreateFence(_device.LogicalDevice, &fenceCreateInfo, null, out _waitFences[i]);
    }

    _semaphores = new Semaphores[Swapchain.Images.Length];
    for (int i = 0; i < Swapchain.Images.Length; i++) {
      _semaphores[i] = new();
      VkSemaphoreCreateInfo semaphoreCreateInfo = new();
      vkCreateSemaphore(_device.LogicalDevice, &semaphoreCreateInfo, null, out _semaphores[i].PresentComplete).CheckResult();
      vkCreateSemaphore(_device.LogicalDevice, &semaphoreCreateInfo, null, out _semaphores[i].RenderComplete).CheckResult();
    }
  }
  private void CreateDepthStencil(int index) {
    var dp = FindDepthFormat();
    DepthFormat = DwarfFormatConverter.AsDwarfFormat(dp);

    VkImageCreateInfo imageCI = new();
    imageCI.imageType = VK_IMAGE_TYPE_2D;
    imageCI.format = dp;
    imageCI.extent = new(Swapchain.Extent2D.Width, Swapchain.Extent2D.Height, 1);
    imageCI.mipLevels = 1;
    imageCI.arrayLayers = 1;
    imageCI.samples = VK_SAMPLE_COUNT_1_BIT;
    imageCI.tiling = VK_IMAGE_TILING_OPTIMAL;
    imageCI.usage = VK_IMAGE_USAGE_DEPTH_STENCIL_ATTACHMENT_BIT | VK_IMAGE_USAGE_SAMPLED_BIT;

    vkCreateImage(_device.LogicalDevice, &imageCI, null, out _depthStencil[index].Image);

    VkMemoryRequirements memReqs = new();
    vkGetImageMemoryRequirements(_device.LogicalDevice, _depthStencil[index].Image, &memReqs);

    VkMemoryAllocateInfo memAllloc = new();
    memAllloc.allocationSize = memReqs.size;
    memAllloc.memoryTypeIndex = _device.FindMemoryType(memReqs.memoryTypeBits, MemoryProperty.DeviceLocal);
    vkAllocateMemory(_device.LogicalDevice, &memAllloc, null, out _depthStencil[index].ImageMemory).CheckResult();
    vkBindImageMemory(_device.LogicalDevice, _depthStencil[index].Image, _depthStencil[index].ImageMemory, 0).CheckResult();

    VkImageViewCreateInfo imageViewCI = new();
    imageViewCI.viewType = VK_IMAGE_VIEW_TYPE_2D;
    imageViewCI.image = _depthStencil[index].Image;
    imageViewCI.format = dp;
    imageViewCI.subresourceRange.baseMipLevel = 0;
    imageViewCI.subresourceRange.levelCount = 1;
    imageViewCI.subresourceRange.baseArrayLayer = 0;
    imageViewCI.subresourceRange.layerCount = 1;
    imageViewCI.subresourceRange.aspectMask = VK_IMAGE_ASPECT_DEPTH_BIT;
    // Stencil aspect should only be set on depth + stencil formats (VK_FORMAT_D16_UNORM_S8_UINT..VK_FORMAT_D32_SFLOAT_S8_UINT
    if (dp >= VK_FORMAT_D16_UNORM_S8_UINT) {
      // imageViewCI.subresourceRange.aspectMask |= VK_IMAGE_ASPECT_STENCIL_BIT;
    }
    vkCreateImageView(_device.LogicalDevice, &imageViewCI, null, out _depthStencil[index].ImageView).CheckResult();
  }

  // private void CreateColor(int index) {
  //   VkImageCreateInfo imageCI = new();
  //   imageCI.imageType = VK_IMAGE_TYPE_2D;
  //   imageCI.format = _swapchain.ColorFormat.AsVkFormat();
  //   imageCI.extent = new(Swapchain.Extent2D.Width, Swapchain.Extent2D.Height, 1);
  //   imageCI.mipLevels = 1;
  //   imageCI.arrayLayers = 1;
  //   imageCI.samples = VK_SAMPLE_COUNT_1_BIT;
  //   imageCI.tiling = VK_IMAGE_TILING_OPTIMAL;
  //   imageCI.usage = VK_IMAGE_USAGE_COLOR_ATTACHMENT_BIT | VK_IMAGE_USAGE_SAMPLED_BIT;

  //   vkCreateImage(_device.LogicalDevice, &imageCI, null, out _colorImage[index].Image);

  //   VkMemoryRequirements memReqs = new();
  //   vkGetImageMemoryRequirements(_device.LogicalDevice, _colorImage[index].Image, &memReqs);

  //   VkMemoryAllocateInfo memAllloc = new();
  //   memAllloc.allocationSize = memReqs.size;
  //   memAllloc.memoryTypeIndex = _device.FindMemoryType(memReqs.memoryTypeBits, MemoryProperty.DeviceLocal);
  //   vkAllocateMemory(_device.LogicalDevice, &memAllloc, null, out _colorImage[index].ImageMemory).CheckResult();
  //   vkBindImageMemory(_device.LogicalDevice, _colorImage[index].Image, _colorImage[index].ImageMemory, 0).CheckResult();

  //   VkImageViewCreateInfo imageViewCI = new();
  //   imageViewCI.viewType = VK_IMAGE_VIEW_TYPE_2D;
  //   imageViewCI.image = _colorImage[index].Image;
  //   imageViewCI.format = _swapchain.ColorFormat.AsVkFormat();
  //   imageViewCI.subresourceRange.baseMipLevel = 0;
  //   imageViewCI.subresourceRange.levelCount = 1;
  //   imageViewCI.subresourceRange.baseArrayLayer = 0;
  //   imageViewCI.subresourceRange.layerCount = 1;
  //   imageViewCI.subresourceRange.aspectMask = VK_IMAGE_ASPECT_COLOR_BIT;
  //   // Stencil aspect should only be set on depth + stencil formats (VK_FORMAT_D16_UNORM_S8_UINT..VK_FORMAT_D32_SFLOAT_S8_UINT
  //   vkCreateImageView(_device.LogicalDevice, &imageViewCI, null, out _colorImage[index].ImageView).CheckResult();
  // }

  private VkFormat FindDepthFormat() {
    var items = new List<VkFormat> {
      VkFormat.D32Sfloat,
      VkFormat.D32SfloatS8Uint,
      VkFormat.D24UnormS8Uint
    };
    return _device.FindSupportedFormat(items, VkImageTiling.Optimal, VkFormatFeatureFlags.DepthStencilAttachment);
  }

  public void RecreateSwapchain() {
    var extent = _window.Extent.ToVkExtent2D();
    while (extent.width == 0 || extent.height == 0 || _window.IsMinimalized) {
      extent = _window.Extent.ToVkExtent2D();
    }

    _device.WaitDevice();

    if (Swapchain != null) {
      if (_depthStencil.Length > 0) {
        for (int i = 0; i < Swapchain.Images.Length; i++) {
          vkDestroyImageView(_device.LogicalDevice, _depthStencil[i].ImageView, null);
          vkDestroyImage(_device.LogicalDevice, _depthStencil[i].Image, null);
          vkFreeMemory(_device.LogicalDevice, _depthStencil[i].ImageMemory, null);
        }
      }
    }

    Swapchain?.Dispose();
    _swapchain = new VulkanDynamicSwapchain(_device, extent, _application.VSync);
    if (_depthStencil.Length < 1) {
      _depthStencil = new AttachmentImage[_swapchain.Images.Length];
      _sceneColor = new AttachmentImage[_swapchain.Images.Length];
      // _colorImage = new AttachmentImage[_swapchain.Images.Length];
      for (int i = 0; i < _swapchain.Images.Length; i++) {
        _depthStencil[i] = new();
        _sceneColor[i] = new();
        // _colorImage[i] = new();
      }
    }
    for (int i = 0; i < _swapchain.Images.Length; i++) {
      CreateDepthStencil(i);
      CreateSceneColor(i);
      // CreateColor(i);
    }

    Logger.Info("Recreated Swapchain");
  }
  public ulong GetSwapchainRenderPass() {
    return VkRenderPass.Null;
  }

  public ulong GetPostProcessingPass() {
    return VkRenderPass.Null;
  }
  public nint BeginFrame(CommandBufferLevel level = CommandBufferLevel.Primary) {
    if (IsFrameInProgress) {
      Logger.Error("Cannot start frame while already in progress!");
      return VkCommandBuffer.Null;
    }

    PrepareFrame();

    IsFrameInProgress = true;

    var commandBuffer = _commandBuffers[_imageIndex];
    vkResetCommandBuffer(commandBuffer, VkCommandBufferResetFlags.None);
    VkCommandBufferBeginInfo beginInfo = new();
    if (level == CommandBufferLevel.Secondary) {
      beginInfo.flags = VkCommandBufferUsageFlags.SimultaneousUse;
    }
    vkBeginCommandBuffer(commandBuffer, &beginInfo);

    return commandBuffer;
  }

  public void EndFrame() {
    if (!IsFrameInProgress) {
      Logger.Error("Cannot end frame is not in progress!");
    }

    var commandBuffer = _commandBuffers[_imageIndex];
    vkEndCommandBuffer(commandBuffer).CheckResult();

    fixed (VkSemaphore* renderPtr = &_semaphores[Swapchain.CurrentFrame].RenderComplete)
    fixed (VkSemaphore* presentPtr = &_semaphores[Swapchain.CurrentFrame].PresentComplete) {
      VkSubmitInfo submitInfo = new();

      VkPipelineStageFlags* waitStages = stackalloc VkPipelineStageFlags[1];
      waitStages[0] = VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT;

      submitInfo.waitSemaphoreCount = 1;
      submitInfo.pWaitSemaphores = presentPtr;
      submitInfo.pWaitDstStageMask = waitStages;

      submitInfo.commandBufferCount = 1;
      submitInfo.pCommandBuffers = &commandBuffer;

      submitInfo.signalSemaphoreCount = 1;
      submitInfo.pSignalSemaphores = renderPtr;
      submitInfo.pNext = null;

      vkResetFences(_device.LogicalDevice, _waitFences[Swapchain.CurrentFrame]);

      Application.Mutex.WaitOne();
      var queueResult = vkQueueSubmit(_device.GraphicsQueue, 1, &submitInfo, _waitFences[Swapchain.CurrentFrame]);
      Application.Mutex.ReleaseMutex();
      SubmitFrame();


      // VkPresentWait2InfoKHR vkPresentWait2Info = new();
      // vkPresentWait2Info.presentId = _imageIndex;
      // vkPresentWait2Info.
      // vkWaitForPresent2KHR(_device.LogicalDevice, Swapchain, )

      IsFrameInProgress = false;
    }
  }

  public void BeginPostProcess(nint commandBuffer) {
    VkUtils.InsertMemoryBarrier2(
      CurrentCommandBuffer, Swapchain.Images[_imageIndex], Swapchain.SurfaceFormat,
      VkImageLayout.PresentSrcKHR, VkImageLayout.ColorAttachmentOptimal,
      VkPipelineStageFlags2.None, VkPipelineStageFlags2.ColorAttachmentOutput,
      VkAccessFlags2.None, VkAccessFlags2.ColorAttachmentWrite
    );

    VkRenderingAttachmentInfo colorAttachment = new();
    colorAttachment.imageView = Swapchain.ImageViews[_imageIndex];
    colorAttachment.imageLayout = VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL;
    colorAttachment.loadOp = VK_ATTACHMENT_LOAD_OP_CLEAR;
    colorAttachment.storeOp = VK_ATTACHMENT_STORE_OP_STORE;
    colorAttachment.clearValue.color = new(0.137f, 0.137f, 0.137f, 1.0f);

    VkRenderingAttachmentInfo depthStencilAttachment = new();
    depthStencilAttachment.imageView = _depthStencil[_imageIndex].ImageView;
    depthStencilAttachment.imageLayout = VK_IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL;
    depthStencilAttachment.loadOp = VK_ATTACHMENT_LOAD_OP_CLEAR;
    depthStencilAttachment.storeOp = VK_ATTACHMENT_STORE_OP_STORE;
    depthStencilAttachment.clearValue.depthStencil = new(1.0f, 0);
    depthStencilAttachment.resolveMode = VkResolveModeFlags.None;
    depthStencilAttachment.resolveImageView = VkImageView.Null;
    depthStencilAttachment.resolveImageLayout = VkImageLayout.Undefined;

    VkRenderingInfo renderingInfo = new();
    renderingInfo.renderArea = new(0, 0, Swapchain.Extent2D.Width, Swapchain.Extent2D.Height);
    renderingInfo.layerCount = 1;
    renderingInfo.colorAttachmentCount = 1;
    renderingInfo.pColorAttachments = &colorAttachment;
    renderingInfo.pDepthAttachment = &depthStencilAttachment;
    renderingInfo.viewMask = 0;
    // renderingInfo.pStencilAttachment = &depthStencilAttachment;

    vkCmdBeginRendering(commandBuffer, &renderingInfo);

    VkViewport viewport = new() {
      x = 0.0f,
      y = 0.0f,
      width = Swapchain.Extent2D.Width,
      height = Swapchain.Extent2D.Height,
      minDepth = 0.0f,
      maxDepth = 1.0f
    };

    VkRect2D scissor = new(0, 0, Swapchain.Extent2D.Width, Swapchain.Extent2D.Height);
    vkCmdSetViewport(commandBuffer, 0, 1, &viewport);
    vkCmdSetScissor(commandBuffer, 0, 1, &scissor);
  }

  public void EndPostProcess(nint commandBuffer) {
    vkCmdEndRendering(commandBuffer);

    VkUtils.InsertMemoryBarrier2(
      CurrentCommandBuffer, Swapchain.Images[_imageIndex], Swapchain.SurfaceFormat,
      VkImageLayout.ColorAttachmentOptimal, VkImageLayout.PresentSrcKHR,
      VkPipelineStageFlags2.ColorAttachmentOutput, VkPipelineStageFlags2.None,
      VkAccessFlags2.ColorAttachmentWrite, VkAccessFlags2.None
    );
  }

  public void BeginRendering(nint commandBuffer) {
    if (!IsFrameInProgress) {
      Logger.Error("Cannot start render pass while already in progress!");
      return;
    }
    if (commandBuffer != CurrentCommandBuffer) {
      Logger.Error("Can't begin render pass on command buffer from diffrent frame!");
      return;
    }

    // VkUtils.InsertMemoryBarrier(
    //   cmdbuffer: commandBuffer,
    //   image: Swapchain.Images[_imageIndex],
    //   format: Swapchain.SurfaceFormat,
    //   oldImageLayout: VkImageLayout.Undefined,
    //   newImageLayout: VkImageLayout.ColorAttachmentOptimal
    // );

    // SceneColor: (UNDEFINED or SHADER_READ_ONLY) -> COLOR_ATTACHMENT
    VkUtils.InsertMemoryBarrier2(
      CurrentCommandBuffer, _sceneColor[_imageIndex].Image, Swapchain.SurfaceFormat,
      VkImageLayout.Undefined, VkImageLayout.ColorAttachmentOptimal,
      VkPipelineStageFlags2.None, VkPipelineStageFlags2.ColorAttachmentOutput,
      VkAccessFlags2.None, VkAccessFlags2.ColorAttachmentWrite
    );

    // Depth: UNDEFINED -> DEPTH_ATTACHMENT
    VkUtils.InsertMemoryBarrier2(
      CurrentCommandBuffer, _depthStencil[_imageIndex].Image, DepthFormat.AsVkFormat(),
      VkImageLayout.Undefined, VkImageLayout.DepthAttachmentOptimal,
      VkPipelineStageFlags2.None, VkPipelineStageFlags2.EarlyFragmentTests | VkPipelineStageFlags2.LateFragmentTests,
      VkAccessFlags2.None, VkAccessFlags2.DepthStencilAttachmentWrite
    );

    // VkCommandBufferBeginInfo cmdBufInfo = new();
    // VkUtils.InsertMemoryBarrier(
    //     commandBuffer,
    //     Swapchain.Images[_imageIndex],
    //     0,
    //     VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT,
    //     VK_IMAGE_LAYOUT_UNDEFINED,
    //     VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
    //     VK_PIPELINE_STAGE_TOP_OF_PIPE_BIT,
    //     VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT,
    //     new VkImageSubresourceRange(VK_IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1)
    //   );

    // VkUtils.InsertMemoryBarrier(
    //   commandBuffer,
    //   _depthStencil[_imageIndex].Image,
    //   0,
    //   VK_ACCESS_DEPTH_STENCIL_ATTACHMENT_WRITE_BIT,
    //   VK_IMAGE_LAYOUT_UNDEFINED,
    //   VK_IMAGE_LAYOUT_DEPTH_ATTACHMENT_OPTIMAL,
    //   VK_PIPELINE_STAGE_EARLY_FRAGMENT_TESTS_BIT | VK_PIPELINE_STAGE_LATE_FRAGMENT_TESTS_BIT,
    //   VK_PIPELINE_STAGE_EARLY_FRAGMENT_TESTS_BIT | VK_PIPELINE_STAGE_LATE_FRAGMENT_TESTS_BIT,
    //   new VkImageSubresourceRange(VK_IMAGE_ASPECT_DEPTH_BIT, 0, 1, 0, 1)
    // );

    VkRenderingAttachmentInfo colorAttachment = new();
    colorAttachment.imageView = _sceneColor[_imageIndex].ImageView;
    colorAttachment.imageLayout = VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL;
    colorAttachment.loadOp = VK_ATTACHMENT_LOAD_OP_CLEAR;
    colorAttachment.storeOp = VK_ATTACHMENT_STORE_OP_STORE;
    colorAttachment.clearValue.color = new(0.137f, 0.137f, 0.137f, 1.0f);

    VkRenderingAttachmentInfo depthStencilAttachment = new();
    depthStencilAttachment.imageView = _depthStencil[_imageIndex].ImageView;
    depthStencilAttachment.imageLayout = VK_IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL;
    depthStencilAttachment.loadOp = VK_ATTACHMENT_LOAD_OP_CLEAR;
    depthStencilAttachment.storeOp = VK_ATTACHMENT_STORE_OP_STORE;
    depthStencilAttachment.clearValue.depthStencil = new(1.0f, 0);
    depthStencilAttachment.resolveMode = VkResolveModeFlags.None;
    depthStencilAttachment.resolveImageView = VkImageView.Null;
    depthStencilAttachment.resolveImageLayout = VkImageLayout.Undefined;

    VkRenderingInfo renderingInfo = new();
    renderingInfo.renderArea = new(0, 0, Swapchain.Extent2D.Width, Swapchain.Extent2D.Height);
    renderingInfo.layerCount = 1;
    renderingInfo.colorAttachmentCount = 1;
    renderingInfo.pColorAttachments = &colorAttachment;
    renderingInfo.pDepthAttachment = &depthStencilAttachment;
    renderingInfo.viewMask = 0;
    // renderingInfo.pStencilAttachment = &depthStencilAttachment;

    vkCmdBeginRendering(commandBuffer, &renderingInfo);

    VkViewport viewport = new() {
      x = 0.0f,
      y = 0.0f,
      width = Swapchain.Extent2D.Width,
      height = Swapchain.Extent2D.Height,
      minDepth = 0.0f,
      maxDepth = 1.0f
    };

    VkRect2D scissor = new(0, 0, Swapchain.Extent2D.Width, Swapchain.Extent2D.Height);
    vkCmdSetViewport(commandBuffer, 0, 1, &viewport);
    vkCmdSetScissor(commandBuffer, 0, 1, &scissor);
  }

  public void EndRendering(nint commandBuffer) {
    if (!IsFrameInProgress) {
      Logger.Error("Cannot end render pass on not started frame!");
      return;
    }
    if (commandBuffer != CurrentCommandBuffer) {
      Logger.Error("Can't end render pass on command buffer from diffrent frame!");
      return;
    }

    vkCmdEndRendering(commandBuffer);

    // VkUtils.InsertMemoryBarrier(
    //   cmdbuffer: commandBuffer,
    //   image: Swapchain.Images[_imageIndex],
    //   format: Swapchain.SurfaceFormat,
    //   oldImageLayout: VkImageLayout.ColorAttachmentOptimal,
    //   newImageLayout: VkImageLayout.PresentSrcKHR
    // );

    // VkUtils.InsertMemoryBarrier2(
    //   commandBuffer,
    //   Swapchain.Images[_imageIndex],
    //   Swapchain.SurfaceFormat,
    //   oldImageLayout: VkImageLayout.ColorAttachmentOptimal,
    //   newImageLayout: VkImageLayout.PresentSrcKHR,
    //   VkPipelineStageFlags2.ColorAttachmentOutput,
    //   VkPipelineStageFlags2.Transfer,
    //   VkAccessFlags2.ColorAttachmentWrite,
    //   VkAccessFlags2.TransferRead
    // );

    VkUtils.InsertMemoryBarrier2(
      CurrentCommandBuffer, _sceneColor[_imageIndex].Image, Swapchain.SurfaceFormat,
      VkImageLayout.ColorAttachmentOptimal, VkImageLayout.ShaderReadOnlyOptimal,
      VkPipelineStageFlags2.ColorAttachmentOutput, VkPipelineStageFlags2.FragmentShader,
      VkAccessFlags2.ColorAttachmentWrite, VkAccessFlags2.ShaderRead
    );

    // Depth (if sampling): DEPTH_ATTACHMENT -> DEPTH_READ_ONLY
    VkUtils.InsertMemoryBarrier2(
      CurrentCommandBuffer, _depthStencil[_imageIndex].Image, DepthFormat.AsVkFormat(),
      VkImageLayout.DepthAttachmentOptimal, VkImageLayout.DepthReadOnlyOptimal,
      VkPipelineStageFlags2.EarlyFragmentTests | VkPipelineStageFlags2.LateFragmentTests,
      VkPipelineStageFlags2.FragmentShader,
      VkAccessFlags2.DepthStencilAttachmentWrite, VkAccessFlags2.ShaderRead
    );

    /*

    // 1) After vkCmdEndRendering on the swapchain image, move it to TRANSFER_SRC
imgBarrier2(src,
    VK_IMAGE_LAYOUT_ATTACHMENT_OPTIMAL_KHR, VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
    VK_PIPELINE_STAGE_2_COLOR_ATTACHMENT_OUTPUT_BIT, VK_ACCESS_2_COLOR_ATTACHMENT_WRITE_BIT,
    VK_PIPELINE_STAGE_2_TRANSFER_BIT,            VK_ACCESS_2_TRANSFER_READ_BIT);

// 2a) If copying to your GPU image
imgBarrier2(dstImage,
    VK_IMAGE_LAYOUT_UNDEFINED, VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
    VK_PIPELINE_STAGE_2_TOP_OF_PIPE_BIT, 0,
    VK_PIPELINE_STAGE_2_TRANSFER_BIT,    VK_ACCESS_2_TRANSFER_WRITE_BIT);


    */

    // VkUtils.InsertMemoryBarrier(
    //   commandBuffer,
    //   Swapchain.Images[_imageIndex],
    //   VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT,
    //   0,
    //   VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
    //   VK_IMAGE_LAYOUT_PRESENT_SRC_KHR,
    //   VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT,
    //   VK_PIPELINE_STAGE_BOTTOM_OF_PIPE_BIT,
    //   new VkImageSubresourceRange(VK_IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1)
    // );

    // VkUtils.InsertMemoryBarrier(
    //   commandBuffer,
    //   _depthStencil[_imageIndex].Image,
    //   VK_ACCESS_DEPTH_STENCIL_ATTACHMENT_WRITE_BIT,
    //   0,
    //   VK_IMAGE_LAYOUT_DEPTH_ATTACHMENT_OPTIMAL,
    //   VK_IMAGE_LAYOUT_PRESENT_SRC_KHR,
    //   VK_PIPELINE_STAGE_EARLY_FRAGMENT_TESTS_BIT | VK_PIPELINE_STAGE_LATE_FRAGMENT_TESTS_BIT,
    //   VK_PIPELINE_STAGE_BOTTOM_OF_PIPE_BIT,
    //   new VkImageSubresourceRange(VK_IMAGE_ASPECT_DEPTH_BIT, 0, 1, 0, 1)
    // );
  }

  public void CreateCommandBuffers(ulong commandPool, CommandBufferLevel level = CommandBufferLevel.Primary) {
    _commandBuffers = new VkCommandBuffer[Swapchain.Images.Length];

    VkCommandBufferAllocateInfo cmdBufAllocateInfo = new() {
      commandPool = commandPool,
      level = (VkCommandBufferLevel)level,
      commandBufferCount = (uint)_commandBuffers.Length
    };

    fixed (VkCommandBuffer* cmdBuffersPtr = _commandBuffers) {
      vkAllocateCommandBuffers(_device.LogicalDevice, &cmdBufAllocateInfo, cmdBuffersPtr).CheckResult();
    }
  }

  private void CreateSceneColor(int index) {
    var colorFormat = _swapchain.ColorFormat.AsVkFormat(); // or a linear format if you tonemap in PP
    VkImageCreateInfo imageCI = new();
    imageCI.imageType = VK_IMAGE_TYPE_2D;
    imageCI.format = colorFormat;
    imageCI.extent = new(Swapchain.Extent2D.Width, Swapchain.Extent2D.Height, 1);
    imageCI.mipLevels = 1;
    imageCI.arrayLayers = 1;
    imageCI.samples = VK_SAMPLE_COUNT_1_BIT;
    imageCI.tiling = VK_IMAGE_TILING_OPTIMAL;
    imageCI.usage = VK_IMAGE_USAGE_COLOR_ATTACHMENT_BIT | VK_IMAGE_USAGE_SAMPLED_BIT;

    vkCreateImage(_device.LogicalDevice, &imageCI, null, out _sceneColor[index].Image).CheckResult();

    VkMemoryRequirements memReqs;
    vkGetImageMemoryRequirements(_device.LogicalDevice, _sceneColor[index].Image, &memReqs);

    VkMemoryAllocateInfo alloc = new();
    alloc.allocationSize = memReqs.size;
    alloc.memoryTypeIndex = _device.FindMemoryType(memReqs.memoryTypeBits, MemoryProperty.DeviceLocal);
    vkAllocateMemory(_device.LogicalDevice, &alloc, null, out _sceneColor[index].ImageMemory).CheckResult();
    vkBindImageMemory(_device.LogicalDevice, _sceneColor[index].Image, _sceneColor[index].ImageMemory, 0).CheckResult();

    VkImageViewCreateInfo viewCI = new();
    viewCI.viewType = VK_IMAGE_VIEW_TYPE_2D;
    viewCI.image = _sceneColor[index].Image;
    viewCI.format = colorFormat;
    viewCI.subresourceRange = new(VK_IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1);
    vkCreateImageView(_device.LogicalDevice, &viewCI, null, out _sceneColor[index].ImageView).CheckResult();
  }

  public unsafe void Dispose() {
    _descriptorPool?.Dispose();
    _postProcessLayout?.Dispose();


    for (int i = 0; i < Swapchain.Images.Length; i++) {
      vkDestroySemaphore(_device.LogicalDevice, _semaphores[i].PresentComplete, null);
      vkDestroySemaphore(_device.LogicalDevice, _semaphores[i].RenderComplete, null);

      vkDestroyImageView(_device.LogicalDevice, _depthStencil[i].ImageView, null);
      vkDestroyImage(_device.LogicalDevice, _depthStencil[i].Image, null);
      vkFreeMemory(_device.LogicalDevice, _depthStencil[i].ImageMemory, null);
    }

    foreach (var fence in _waitFences) {
      vkDestroyFence(_device.LogicalDevice, fence, null);
    }

    vkDestroySampler(_device.LogicalDevice, ImageSampler);
    vkDestroySampler(_device.LogicalDevice, DepthSampler);

    Swapchain?.Dispose();
  }

  public ulong PostProcessDecriptor => ImageDescriptors[ImageIndex];
  public ulong PreviousPostProcessDescriptor => ImageDescriptors[Swapchain.PreviousFrame];
  public nint CurrentCommandBuffer => _commandBuffers[_imageIndex];
  public int FrameIndex { get; private set; }
  public int ImageIndex => (int)_imageIndex;
  public float AspectRatio => Swapchain.ExtentAspectRatio();
  public DwarfExtent2D Extent2D => Swapchain.Extent2D;
  public int MAX_FRAMES_IN_FLIGHT => Swapchain.Images.Length;
  public ISwapchain Swapchain => _swapchain;
  public CommandList CommandList { get; } = null!;
  public bool IsFrameInProgress { get; private set; } = false;
  public bool IsFrameStarted => IsFrameInProgress;
}