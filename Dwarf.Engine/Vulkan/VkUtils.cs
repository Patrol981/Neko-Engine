using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Vulkan;

public ref struct SwapChainSupportDetails {
  public VkSurfaceCapabilitiesKHR Capabilities;
  public ReadOnlySpan<VkSurfaceFormatKHR> Formats;
  public ReadOnlySpan<VkPresentModeKHR> PresentModes;
}

public ref struct SwapChainSupportDetails2 {
  public VkSurfaceCapabilities2KHR Capabilities;
  public ReadOnlySpan<VkSurfaceFormat2KHR> Formats;
  public ReadOnlySpan<VkPresentModeKHR> PresentModes;
}

public static class VkUtils {
  public static SwapChainSupportDetails QuerySwapChainSupport(VkPhysicalDevice physicalDevice, VkSurfaceKHR surface) {
    SwapChainSupportDetails details = new SwapChainSupportDetails();
    vkGetPhysicalDeviceSurfaceCapabilitiesKHR(physicalDevice, surface, out details.Capabilities).CheckResult();

    details.Formats = vkGetPhysicalDeviceSurfaceFormatsKHR(physicalDevice, surface);
    details.PresentModes = vkGetPhysicalDeviceSurfacePresentModesKHR(physicalDevice, surface);
    return details;
  }

  public static unsafe SwapChainSupportDetails2 QuerySwapChainSupport2(
    VkPhysicalDevice physicalDevice,
    VkSurfaceKHR surface) {
    var details = new SwapChainSupportDetails2 {
      Capabilities = new(),
      Formats = new(),
      PresentModes = new()
    };

    var surfaceInfo = new VkPhysicalDeviceSurfaceInfo2KHR() {
      sType = VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_SURFACE_INFO_2_KHR,
      pNext = null,
      surface = surface
    };
    vkGetPhysicalDeviceSurfaceCapabilities2KHR(physicalDevice, &surfaceInfo, &details.Capabilities);

    uint formatCount = 0;
    vkGetPhysicalDeviceSurfaceFormats2KHR(physicalDevice, &surfaceInfo, &formatCount, null);
    var formats = new VkSurfaceFormat2KHR[formatCount];
    for (int i = 0; i < formats.Length; i++) {
      formats[i] = new();
    }
    fixed (VkSurfaceFormat2KHR* pFormats = formats) {
      vkGetPhysicalDeviceSurfaceFormats2KHR(physicalDevice, &surfaceInfo, &formatCount, pFormats);
    }
    details.Formats = formats;

    // uint presentCount = 0;
    details.PresentModes = vkGetPhysicalDeviceSurfacePresentModesKHR(physicalDevice, surface);
    // vkGetPhysicalDeviceSurfacePresentModes2EXT(physicalDevice, &surfaceInfo, &presentCount, null).CheckResult();

    // var presentModes = new VkPresentModeKHR[presentCount];
    // fixed (VkPresentModeKHR* pPresent = presentModes) {
    //   vkGetPhysicalDeviceSurfacePresentModes2EXT(
    //       physicalDevice,
    //       &surfaceInfo,
    //       &presentCount,
    //       pPresent);
    // }
    // details.PresentModes = presentModes;

    return details;
  }

  public static void SetImageLayout(
    VkCommandBuffer commandBuffer,
    VkImage image,
    VkImageAspectFlags aspectMask,
    VkImageLayout oldImageLayout,
    VkImageLayout newImageLayout,
    VkPipelineStageFlags srcStageFlags = VkPipelineStageFlags.AllCommands,
    VkPipelineStageFlags dstStageFlags = VkPipelineStageFlags.AllCommands
  ) {
    VkImageSubresourceRange subresourceRange = new();
    subresourceRange.aspectMask = aspectMask;
    subresourceRange.baseMipLevel = 0;
    subresourceRange.levelCount = 1;
    subresourceRange.layerCount = 1;
    SetImageLayout(commandBuffer, image, oldImageLayout, newImageLayout, subresourceRange, srcStageFlags, dstStageFlags);
  }

  public static void SetImageLayout(
    VkCommandBuffer commandBuffer,
    VkImage image,
    VkImageLayout oldImageLayout,
    VkImageLayout newImageLayout,
    VkImageSubresourceRange subresourceRange,
    VkPipelineStageFlags srcStageFlags = VkPipelineStageFlags.AllCommands,
    VkPipelineStageFlags dstStageFlags = VkPipelineStageFlags.AllCommands
  ) {
    var imageMemoryBarrier = new VkImageMemoryBarrier();
    imageMemoryBarrier.srcQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED;
    imageMemoryBarrier.dstQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED;
    imageMemoryBarrier.oldLayout = oldImageLayout;
    imageMemoryBarrier.newLayout = newImageLayout;
    imageMemoryBarrier.image = image;
    imageMemoryBarrier.subresourceRange = subresourceRange;

    _ = oldImageLayout switch {
      VkImageLayout.Undefined => imageMemoryBarrier.srcAccessMask = 0,
      VkImageLayout.Preinitialized => imageMemoryBarrier.srcAccessMask = VkAccessFlags.HostWrite,
      VkImageLayout.ColorAttachmentOptimal => imageMemoryBarrier.srcAccessMask = VkAccessFlags.ColorAttachmentWrite,
      VkImageLayout.DepthStencilAttachmentOptimal => imageMemoryBarrier.srcAccessMask = VkAccessFlags.DepthStencilAttachmentWrite,
      VkImageLayout.TransferSrcOptimal => imageMemoryBarrier.srcAccessMask = VkAccessFlags.TransferRead,
      VkImageLayout.TransferDstOptimal => imageMemoryBarrier.srcAccessMask = VkAccessFlags.TransferWrite,
      VkImageLayout.ShaderReadOnlyOptimal => imageMemoryBarrier.srcAccessMask = VkAccessFlags.ShaderRead,
      _ => imageMemoryBarrier.dstAccessMask = VkAccessFlags.None
    };

    _ = newImageLayout switch {
      VkImageLayout.TransferDstOptimal => imageMemoryBarrier.dstAccessMask = VkAccessFlags.TransferWrite,
      VkImageLayout.TransferSrcOptimal => imageMemoryBarrier.dstAccessMask = VkAccessFlags.TransferRead,
      VkImageLayout.ColorAttachmentOptimal => imageMemoryBarrier.dstAccessMask = VkAccessFlags.ColorAttachmentWrite,
      VkImageLayout.DepthStencilAttachmentOptimal => imageMemoryBarrier.dstAccessMask |= VkAccessFlags.DepthStencilAttachmentWrite,
      _ => imageMemoryBarrier.dstAccessMask = VkAccessFlags.None
    };

    if (newImageLayout == VkImageLayout.ShaderReadOnlyOptimal) {
      if (imageMemoryBarrier.srcAccessMask == 0) {
        imageMemoryBarrier.srcAccessMask = VkAccessFlags.HostWrite | VkAccessFlags.TransferWrite;
      }
      imageMemoryBarrier.dstAccessMask = VkAccessFlags.ShaderRead;
    }

    unsafe {
      vkCmdPipelineBarrier(
        commandBuffer,
        srcStageFlags,
        dstStageFlags,
        0,
        0, null,
        0, null,
        1, &imageMemoryBarrier
     );
    }
  }

  public static VkViewport Viewport(float x, float y, float width, float height, float minDepth, float maxDepth) {
    VkViewport viewport = new();
    viewport.width = width;
    viewport.height = height;
    viewport.minDepth = minDepth;
    viewport.maxDepth = maxDepth;
    viewport.x = x;
    viewport.y = y;
    return viewport;
  }

  public static VkDescriptorPoolSize DescriptorPoolSize(VkDescriptorType type, uint count) {
    VkDescriptorPoolSize descriptorPoolSize = new();
    descriptorPoolSize.type = type;
    descriptorPoolSize.descriptorCount = count;
    return descriptorPoolSize;
  }

  public static unsafe VkDescriptorPoolCreateInfo DescriptorPoolCreateInfo(
    VkDescriptorPoolSize[] poolSizes,
    uint maxSets
  ) {

    VkDescriptorPoolCreateInfo descriptorPoolInfo = new();
    descriptorPoolInfo.poolSizeCount = (uint)poolSizes.Length;
    fixed (VkDescriptorPoolSize* poolSizesPtr = poolSizes) {
      descriptorPoolInfo.pPoolSizes = poolSizesPtr;
    }
    descriptorPoolInfo.maxSets = maxSets;
    return descriptorPoolInfo;
  }

  public static unsafe VkDescriptorSetLayoutBinding DescriptorSetLayoutBinding(
    VkDescriptorType type,
    VkShaderStageFlags stageFlags,
    uint binding,
    uint descriptorCount = 1
  ) {
    VkDescriptorSetLayoutBinding setLayoutBinding = new();
    setLayoutBinding.descriptorType = type;
    setLayoutBinding.stageFlags = stageFlags;
    setLayoutBinding.binding = binding;
    setLayoutBinding.descriptorCount = descriptorCount;
    return setLayoutBinding;
  }

  public static unsafe VkDescriptorSetLayoutCreateInfo DescriptorSetLayoutCreateInfo(
    VkDescriptorSetLayoutBinding[] bindings
  ) {
    VkDescriptorSetLayoutCreateInfo descriptorSetLayoutCreateInfo = new();
    fixed (VkDescriptorSetLayoutBinding* bindingsPtr = bindings) {
      descriptorSetLayoutCreateInfo.pBindings = bindingsPtr;
    }
    descriptorSetLayoutCreateInfo.bindingCount = (uint)bindings.Length;
    return descriptorSetLayoutCreateInfo;
  }

  public static unsafe VkDescriptorSetAllocateInfo DescriptorSetAllocateInfo(
    VkDescriptorPool descriptorPool,
    VkDescriptorSetLayout* pSetLayouts,
    uint descriptorSetCount
  ) {
    VkDescriptorSetAllocateInfo descriptorSetAllocateInfo = new();
    descriptorSetAllocateInfo.descriptorPool = descriptorPool;
    descriptorSetAllocateInfo.pSetLayouts = pSetLayouts;
    descriptorSetAllocateInfo.descriptorSetCount = descriptorSetCount;
    return descriptorSetAllocateInfo;
  }

  public static unsafe VkDescriptorImageInfo DescriptorImageInfo(
    VkSampler sampler,
    VkImageView imageView,
    VkImageLayout imageLayout
  ) {
    VkDescriptorImageInfo descriptorImageInfo = new();
    descriptorImageInfo.sampler = sampler;
    descriptorImageInfo.imageView = imageView;
    descriptorImageInfo.imageLayout = imageLayout;
    return descriptorImageInfo;
  }

  public static unsafe VkWriteDescriptorSet WriteDescriptorSet(
    VkDescriptorSet dstSet,
    VkDescriptorType type,
    uint binding,
    VkDescriptorImageInfo imageInfo,
    uint descriptorCount = 1
  ) {
    VkWriteDescriptorSet writeDescriptorSet = new();
    writeDescriptorSet.dstSet = dstSet;
    writeDescriptorSet.descriptorType = type;
    writeDescriptorSet.dstBinding = binding;
    writeDescriptorSet.pImageInfo = &imageInfo;
    writeDescriptorSet.descriptorCount = descriptorCount;
    return writeDescriptorSet;
  }

  public static VkPushConstantRange PushConstantRange(VkShaderStageFlags stageFlags, uint size, uint offset) {
    VkPushConstantRange pushConstantRange = new();
    pushConstantRange.stageFlags = stageFlags;
    pushConstantRange.offset = offset;
    pushConstantRange.size = size;
    return pushConstantRange;
  }

  public static unsafe VkPipelineLayoutCreateInfo PipelineLayoutCreateInfo(
    VkDescriptorSetLayout* setLayouts,
    uint count
  ) {
    VkPipelineLayoutCreateInfo pipelineLayoutCreateInfo = new();
    pipelineLayoutCreateInfo.setLayoutCount = count;
    pipelineLayoutCreateInfo.pSetLayouts = setLayouts;
    return pipelineLayoutCreateInfo;
  }

  public static VkPipelineInputAssemblyStateCreateInfo PipelineInputAssemblyStateCreateInfo(
    VkPrimitiveTopology topology,
    VkPipelineInputAssemblyStateCreateFlags flags,
    bool primitiveRestartEnable
  ) {
    VkPipelineInputAssemblyStateCreateInfo pipelineInputAssemblyStateCreateInfo = new();
    pipelineInputAssemblyStateCreateInfo.topology = topology;
    pipelineInputAssemblyStateCreateInfo.flags = flags;
    pipelineInputAssemblyStateCreateInfo.primitiveRestartEnable = primitiveRestartEnable;
    return pipelineInputAssemblyStateCreateInfo;
  }

  public static VkPipelineRasterizationStateCreateInfo PipelineRasterizationStateCreateInfo(
    VkPolygonMode polygonMode,
    VkCullModeFlags cullMode,
    VkFrontFace frontFace,
    VkPipelineRasterizationStateCreateFlags flags = 0
  ) {
    VkPipelineRasterizationStateCreateInfo pipelineRasterizationStateCreateInfo = new();
    pipelineRasterizationStateCreateInfo.polygonMode = polygonMode;
    pipelineRasterizationStateCreateInfo.cullMode = cullMode;
    pipelineRasterizationStateCreateInfo.frontFace = frontFace;
    pipelineRasterizationStateCreateInfo.flags = flags;
    pipelineRasterizationStateCreateInfo.depthClampEnable = false;
    pipelineRasterizationStateCreateInfo.lineWidth = 1.0f;
    return pipelineRasterizationStateCreateInfo;
  }

  public static unsafe VkPipelineColorBlendStateCreateInfo PipelineColorBlendStateCreateInfo(
    uint count,
    VkPipelineColorBlendAttachmentState* pAttachments
  ) {
    VkPipelineColorBlendStateCreateInfo pipelineColorBlendStateCreateInfo = new();
    pipelineColorBlendStateCreateInfo.attachmentCount = count;
    pipelineColorBlendStateCreateInfo.pAttachments = pAttachments;
    return pipelineColorBlendStateCreateInfo;
  }

  public static unsafe VkPipelineDepthStencilStateCreateInfo PipelineDepthStencilStateCreateInfo(
    bool depthTestEnable,
    bool depthWriteEnable,
    VkCompareOp depthCompareOp
  ) {
    VkPipelineDepthStencilStateCreateInfo pipelineDepthStencilStateCreateInfo = new();
    pipelineDepthStencilStateCreateInfo.depthTestEnable = depthTestEnable;
    pipelineDepthStencilStateCreateInfo.depthWriteEnable = depthWriteEnable;
    pipelineDepthStencilStateCreateInfo.depthCompareOp = depthCompareOp;
    pipelineDepthStencilStateCreateInfo.back.compareOp = VkCompareOp.Always;
    return pipelineDepthStencilStateCreateInfo;
  }

  public static unsafe VkPipelineViewportStateCreateInfo PipelineViewportStateCreateInfo(
    uint viewportCount,
    uint scissorCount,
    VkPipelineViewportStateCreateFlags flags = VkPipelineViewportStateCreateFlags.None
  ) {
    VkPipelineViewportStateCreateInfo pipelineViewportStateCreateInfo = new();
    pipelineViewportStateCreateInfo.viewportCount = viewportCount;
    pipelineViewportStateCreateInfo.scissorCount = scissorCount;
    pipelineViewportStateCreateInfo.flags = flags;
    return pipelineViewportStateCreateInfo;
  }

  public static unsafe VkPipelineMultisampleStateCreateInfo PipelineMultisampleStateCreateInfo(
    VkSampleCountFlags rasterizationSamples,
    VkPipelineMultisampleStateCreateFlags flags = VkPipelineMultisampleStateCreateFlags.None
  ) {
    VkPipelineMultisampleStateCreateInfo pipelineMultisampleStateCreateInfo = new();
    pipelineMultisampleStateCreateInfo.rasterizationSamples = rasterizationSamples;
    pipelineMultisampleStateCreateInfo.flags = flags;
    return pipelineMultisampleStateCreateInfo;
  }

  public static unsafe VkPipelineDynamicStateCreateInfo PipelineDynamicStateCreateInfo(
    VkDynamicState[] dynamicStates,
    VkPipelineDynamicStateCreateFlags flags = VkPipelineDynamicStateCreateFlags.None
  ) {
    VkPipelineDynamicStateCreateInfo pipelineDynamicStateCreateInfo = new();
    fixed (VkDynamicState* pDynamicStates = dynamicStates) {
      pipelineDynamicStateCreateInfo.pDynamicStates = pDynamicStates;
    }
    pipelineDynamicStateCreateInfo.dynamicStateCount = (uint)dynamicStates.Length;
    pipelineDynamicStateCreateInfo.flags = flags;
    return pipelineDynamicStateCreateInfo;
  }

  public static unsafe VkGraphicsPipelineCreateInfo PipelineCreateInfo(
    VkPipelineLayout layout,
    VkRenderPass renderPass,
    VkPipelineRenderingCreateInfo dynamicRenderCreateInfo,
    VkPipelineCreateFlags flags = VkPipelineCreateFlags.None
  ) {
    VkGraphicsPipelineCreateInfo pipelineCreateInfo = new() {
      layout = layout,
      renderPass = renderPass,
      flags = flags,
      basePipelineIndex = -1,
      basePipelineHandle = VkPipeline.Null,
      pNext = &dynamicRenderCreateInfo
    };
    return pipelineCreateInfo;
  }

  public static unsafe VkVertexInputBindingDescription VertexInputBindingDescription(
    uint binding,
    uint stride,
    VkVertexInputRate inputRate
  ) {
    VkVertexInputBindingDescription vInputBindDescription = new();
    vInputBindDescription.binding = binding;
    vInputBindDescription.stride = stride;
    vInputBindDescription.inputRate = inputRate;
    return vInputBindDescription;
  }

  public static unsafe VkVertexInputAttributeDescription VertexInputAttributeDescription(
    uint binding,
    uint location,
    VkFormat format,
    uint offset
  ) {
    VkVertexInputAttributeDescription vInputAttribDescription = new();
    vInputAttribDescription.location = location;
    vInputAttribDescription.binding = binding;
    vInputAttribDescription.format = format;
    vInputAttribDescription.offset = offset;
    return vInputAttribDescription;
  }

  public static unsafe void InsertMemoryBarrier(
    VkCommandBuffer cmdbuffer,
      VkImage image,
      VkAccessFlags srcAccessMask,
      VkAccessFlags dstAccessMask,
      VkImageLayout oldImageLayout,
      VkImageLayout newImageLayout,
      VkPipelineStageFlags srcStageMask,
      VkPipelineStageFlags dstStageMask,
      VkImageSubresourceRange subresourceRange
  ) {
    VkImageMemoryBarrier imageMemoryBarrier = new() {
      srcAccessMask = srcAccessMask,
      dstAccessMask = dstAccessMask,
      oldLayout = oldImageLayout,
      newLayout = newImageLayout,
      image = image,
      subresourceRange = subresourceRange
    };

    vkCmdPipelineBarrier(
      cmdbuffer,
      srcStageMask,
      dstStageMask,
      0,
      0, null,
      0, null,
      1, &imageMemoryBarrier
    );
  }

  public static unsafe void ImageLayoutTransition(
    VkCommandBuffer commandBuffer,
    VkImage image,
    VkPipelineStageFlags srcStageMask,
    VkPipelineStageFlags dstStageMask,
    VkAccessFlags srcAccessMask,
    VkAccessFlags dstAccessMask,
    VkImageLayout oldLayout,
    VkImageLayout newLayout,
    VkImageSubresourceRange subresourceRange
  ) {
    VkImageMemoryBarrier imageMemoryBarrier = new() {
      sType = VK_STRUCTURE_TYPE_IMAGE_MEMORY_BARRIER,
      srcAccessMask = srcAccessMask,
      dstAccessMask = dstAccessMask,
      oldLayout = oldLayout,
      newLayout = newLayout,
      srcQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED,
      dstQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED,
      image = image,
      subresourceRange = subresourceRange
    };

    vkCmdPipelineBarrier(
      commandBuffer,
      srcStageMask,
      dstStageMask,
      0, 0,
      null, 0,
      null, 1,
      &imageMemoryBarrier
    );
  }

  // public static unsafe void ImageLayoutTransition(
  //   VkCommandBuffer commandBuffer,
  //   VkImage image,
  //   VkImageLayout oldLayout,
  //   VkImageLayout newLayout,
  //   VkImageSubresourceRange subresourceRange
  // ) {
  //   VkPipelineStageFlags src_stage_mask = getPipelineStageFlags(old_layout);
  //   VkPipelineStageFlags dst_stage_mask = getPipelineStageFlags(new_layout);
  //   VkAccessFlags src_access_mask = getAccessFlags(old_layout);
  //   VkAccessFlags dst_access_mask = getAccessFlags(new_layout);

  //   image_layout_transition(command_buffer, image, src_stage_mask, dst_stage_mask, src_access_mask, dst_access_mask, old_layout, new_layout, subresource_range);
  // }
}