using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Neko.AbstractionLayer;
using Neko.Extensions.Logging;
using Neko.Math;
using Neko.Rendering;
using Neko.Utils;
using Neko.Vulkan;
using Neko.Windowing;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Neko.Vulkan;

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

  public NekoFormat DepthFormat { get; private set; }
  public VkDescriptorSet[] ImageDescriptors { get; private set; } = [];
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

    CommandList = new VulkanCommandList(_device);

    RecreateSwapchain();

    // InitVulkan();
    // CreateSamplers();
    // CreateDescriptors();
  }
  public void UpdateDescriptors() {
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
    _device.DeviceApi.vkUpdateDescriptorSets(_device.LogicalDevice, 2, writeDescriptorSets, 0, null);
  }

  private void CreateSamplers() {
    CreateSampler(out var imgSampler, compare: false);
    CreateSampler(out var depthSampler, compare: true);

    ImageSampler = imgSampler;
    DepthSampler = depthSampler;
  }

  private void CreateSampler(out VkSampler sampler, bool compare = false) {
    VkPhysicalDeviceProperties2 properties = new();
    _device.InstanceApi.vkGetPhysicalDeviceProperties2(_device.PhysicalDevice, &properties);

    VkSamplerCreateInfo samplerInfo = new();
    samplerInfo.magFilter = VkFilter.Linear;
    samplerInfo.minFilter = VkFilter.Linear;
    samplerInfo.addressModeU = VkSamplerAddressMode.Repeat;
    samplerInfo.addressModeV = VkSamplerAddressMode.Repeat;
    samplerInfo.addressModeW = VkSamplerAddressMode.Repeat;
    samplerInfo.anisotropyEnable = false;
    samplerInfo.maxAnisotropy = properties.properties.limits.maxSamplerAnisotropy;
    samplerInfo.borderColor = VkBorderColor.FloatTransparentBlack;
    samplerInfo.unnormalizedCoordinates = false;
    samplerInfo.compareEnable = compare;
    samplerInfo.compareOp = compare ? VkCompareOp.LessOrEqual : VkCompareOp.Always;
    samplerInfo.mipmapMode = VkSamplerMipmapMode.Linear;

    _device.DeviceApi.vkCreateSampler(_device.LogicalDevice, &samplerInfo, null, out sampler).CheckResult();
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
      .SetPoolFlags(DescriptorPoolCreateFlags.UpdateAfterBind)
      .Build();

    ImageDescriptors = new VkDescriptorSet[Swapchain.ImageViews.Length];
    DepthDescriptors = new VkDescriptorSet[Swapchain.ImageViews.Length];
    for (int i = 0; i < ImageDescriptors.Length; i++) {
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
    _device.DeviceApi.vkAllocateDescriptorSets(_device.LogicalDevice, &allocInfo, &descriptorSet);

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

    _device.DeviceApi.vkUpdateDescriptorSets(_device.LogicalDevice, 2, writeDescriptorSets, 0, null);
    ImageDescriptors[index] = descriptorSet;
  }

  private void PrepareFrame() {
    fixed (VkFence* fences = _waitFences) {
      _device.DeviceApi.vkWaitForFences(_device.LogicalDevice, (uint)Swapchain.Images.Length, fences, true, UInt64.MaxValue);
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
      _semaphores[_imageIndex].RenderComplete,
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
    // _device.DeviceApi.vkQueueWaitIdle(_device.GraphicsQueue).CheckResult();
  }

  private unsafe void InitVulkan() {
    VkFenceCreateInfo fenceCreateInfo = new() {
      flags = VK_FENCE_CREATE_SIGNALED_BIT
    };
    _waitFences = new VkFence[Swapchain.Images.Length];
    for (int i = 0; i < _waitFences.Length; i++) {
      _device.DeviceApi.vkCreateFence(_device.LogicalDevice, &fenceCreateInfo, null, out _waitFences[i]);
    }

    _semaphores = new Semaphores[Swapchain.Images.Length];
    for (int i = 0; i < Swapchain.Images.Length; i++) {
      _semaphores[i] = new();
      VkSemaphoreCreateInfo semaphoreCreateInfo = new();
      _device.DeviceApi.vkCreateSemaphore(_device.LogicalDevice, &semaphoreCreateInfo, null, out _semaphores[i].PresentComplete).CheckResult();
      _device.DeviceApi.vkCreateSemaphore(_device.LogicalDevice, &semaphoreCreateInfo, null, out _semaphores[i].RenderComplete).CheckResult();
    }
  }
  private void CreateDepthStencil(int index) {
    var dp = FindDepthFormat();
    DepthFormat = NekoFormatConverter.AsNekoFormat(dp);

    VkImageCreateInfo imageCI = new();
    imageCI.imageType = VK_IMAGE_TYPE_2D;
    imageCI.format = dp;
    imageCI.extent = new(Swapchain.Extent2D.Width, Swapchain.Extent2D.Height, 1);
    imageCI.mipLevels = 1;
    imageCI.arrayLayers = 1;
    imageCI.samples = VK_SAMPLE_COUNT_1_BIT;
    imageCI.tiling = VK_IMAGE_TILING_OPTIMAL;
    imageCI.usage = VK_IMAGE_USAGE_DEPTH_STENCIL_ATTACHMENT_BIT | VK_IMAGE_USAGE_SAMPLED_BIT;

    _device.DeviceApi.vkCreateImage(_device.LogicalDevice, &imageCI, null, out _depthStencil[index].Image);

    VkMemoryRequirements memReqs = new();
    _device.DeviceApi.vkGetImageMemoryRequirements(_device.LogicalDevice, _depthStencil[index].Image, &memReqs);

    VkMemoryAllocateInfo memAllloc = new();
    memAllloc.allocationSize = memReqs.size;
    memAllloc.memoryTypeIndex = _device.FindMemoryType(memReqs.memoryTypeBits, MemoryProperty.DeviceLocal);
    _device.DeviceApi.vkAllocateMemory(_device.LogicalDevice, &memAllloc, null, out _depthStencil[index].ImageMemory).CheckResult();
    _device.DeviceApi.vkBindImageMemory(_device.LogicalDevice, _depthStencil[index].Image, _depthStencil[index].ImageMemory, 0).CheckResult();

    VkImageViewCreateInfo imageViewCI = new();
    imageViewCI.viewType = VK_IMAGE_VIEW_TYPE_2D;
    imageViewCI.image = _depthStencil[index].Image;
    imageViewCI.format = dp;
    imageViewCI.subresourceRange.baseMipLevel = 0;
    imageViewCI.subresourceRange.levelCount = 1;
    imageViewCI.subresourceRange.baseArrayLayer = 0;
    imageViewCI.subresourceRange.layerCount = 1;
    imageViewCI.subresourceRange.aspectMask = VkUtils.AspectFor(dp);
    // Stencil aspect should only be set on depth + stencil formats (VK_FORMAT_D16_UNORM_S8_UINT..VK_FORMAT_D32_SFLOAT_S8_UINT
    if (dp >= VK_FORMAT_D16_UNORM_S8_UINT) {
      imageViewCI.subresourceRange.aspectMask |= VK_IMAGE_ASPECT_STENCIL_BIT;
    }
    _device.DeviceApi.vkCreateImageView(_device.LogicalDevice, &imageViewCI, null, out _depthStencil[index].ImageView).CheckResult();
  }

  private VkFormat FindDepthFormat() {
    var items = new List<VkFormat> {
      VkFormat.D32Sfloat,
      VkFormat.D32SfloatS8Uint,
      VkFormat.D24UnormS8Uint
    };
    return _device.FindSupportedFormat(items, VkImageTiling.Optimal, VkFormatFeatureFlags.DepthStencilAttachment);
  }

  private void DestroySyncObjects() {
    if (_semaphores != null) {
      foreach (var s in _semaphores) {
        if (s.PresentComplete.Handle != 0)
          _device.DeviceApi.vkDestroySemaphore(_device.LogicalDevice, s.PresentComplete, null);
        if (s.RenderComplete.Handle != 0)
          _device.DeviceApi.vkDestroySemaphore(_device.LogicalDevice, s.RenderComplete, null);
      }
      _semaphores = Array.Empty<Semaphores>();
    }

    if (_waitFences != null) {
      foreach (var f in _waitFences) {
        if (f.Handle != 0)
          _device.DeviceApi.vkDestroyFence(_device.LogicalDevice, f, null);
      }
      _waitFences = Array.Empty<VkFence>();
    }
  }

  private void DestroyDescriptorsAndSamplers() {
    _descriptorPool?.Dispose();
    _descriptorPool = null!;
    _postProcessLayout?.Dispose();
    _postProcessLayout = null!;

    if (ImageSampler.Handle != 0) {
      _device.DeviceApi.vkDestroySampler(_device.LogicalDevice, ImageSampler);
      ImageSampler = VkSampler.Null;
    }
    if (DepthSampler.Handle != 0) {
      _device.DeviceApi.vkDestroySampler(_device.LogicalDevice, DepthSampler);
      DepthSampler = VkSampler.Null;
    }
  }

  public void RecreateSwapchain() {
    var extent = _window.Extent.ToVkExtent2D();
    while (extent.width == 0 || extent.height == 0 || _window.IsMinimalized) {
      extent = _window.Extent.ToVkExtent2D();
    }

    _device.WaitDevice();

    DestroySyncObjects();
    DestroyDescriptorsAndSamplers();

    if (Swapchain != null) {
      if (_depthStencil.Length > 0) {
        for (int i = 0; i < Swapchain.Images.Length; i++) {
          _device.DeviceApi.vkDestroyImageView(_device.LogicalDevice, _depthStencil[i].ImageView, null);
          _device.DeviceApi.vkDestroyImage(_device.LogicalDevice, _depthStencil[i].Image, null);
          _device.DeviceApi.vkFreeMemory(_device.LogicalDevice, _depthStencil[i].ImageMemory, null);

          _device.DeviceApi.vkDestroyImageView(_device.LogicalDevice, _sceneColor[i].ImageView, null);
          _device.DeviceApi.vkDestroyImage(_device.LogicalDevice, _sceneColor[i].Image, null);
          _device.DeviceApi.vkFreeMemory(_device.LogicalDevice, _sceneColor[i].ImageMemory, null);
        }
      }
      Swapchain.Dispose();
    }

    _swapchain = new VulkanDynamicSwapchain(_device, extent, _application.VSync);

    if (_depthStencil.Length < 1) {
      _depthStencil = new AttachmentImage[_swapchain.Images.Length];
      _sceneColor = new AttachmentImage[_swapchain.Images.Length];
      for (int i = 0; i < _swapchain.Images.Length; i++) {
        _depthStencil[i] = new();
        _sceneColor[i] = new();
      }
    }
    for (int i = 0; i < _swapchain.Images.Length; i++) {
      CreateDepthStencil(i);
      CreateSceneColor(i);
    }

    InitVulkan();
    CreateSamplers();
    CreateDescriptors();

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
    _device.DeviceApi.vkResetCommandBuffer(commandBuffer, VkCommandBufferResetFlags.None);
    VkCommandBufferBeginInfo beginInfo = new();
    if (level == CommandBufferLevel.Secondary) {
      beginInfo.flags = VkCommandBufferUsageFlags.SimultaneousUse;
    }
    _device.DeviceApi.vkBeginCommandBuffer(commandBuffer, &beginInfo);

    return commandBuffer;
  }

  public void EndFrame() {
    if (!IsFrameInProgress) {
      Logger.Error("Cannot end frame is not in progress!");
    }

    var commandBuffer = _commandBuffers[_imageIndex];
    _device.DeviceApi.vkEndCommandBuffer(commandBuffer).CheckResult();

    fixed (VkSemaphore* renderPtr = &_semaphores[_imageIndex].RenderComplete)
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

      _device.DeviceApi.vkResetFences(_device.LogicalDevice, _waitFences[Swapchain.CurrentFrame]);

      Application.Mutex.WaitOne();
      var queueResult = _device.DeviceApi.vkQueueSubmit(_device.GraphicsQueue, 1, &submitInfo, _waitFences[Swapchain.CurrentFrame]);
      Application.Mutex.ReleaseMutex();
      SubmitFrame();

      IsFrameInProgress = false;
    }
  }

  public void SubmitSubCommand(nint commandBuffer) {
    var fence = _device.CreateFence(VkFenceCreateFlags.None);

    fixed (VkSemaphore* renderPtr = &_semaphores[_imageIndex].RenderComplete)
    fixed (VkSemaphore* presentPtr = &_semaphores[Swapchain.CurrentFrame].PresentComplete) {
      VkSubmitInfo submitInfo = new();

      VkPipelineStageFlags* waitStages = stackalloc VkPipelineStageFlags[1];
      waitStages[0] = VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT;

      submitInfo.waitSemaphoreCount = 1;
      submitInfo.pWaitSemaphores = presentPtr;
      submitInfo.pWaitDstStageMask = waitStages;

      submitInfo.commandBufferCount = 1;
      submitInfo.pCommandBuffers = (VkCommandBuffer*)commandBuffer;

      submitInfo.signalSemaphoreCount = 1;
      submitInfo.pSignalSemaphores = renderPtr;
      submitInfo.pNext = null;

      Application.Mutex.WaitOne();
      var queueResult = _device.DeviceApi.vkQueueSubmit(_device.GraphicsQueue, 1, &submitInfo, _waitFences[Swapchain.CurrentFrame]);
      Application.Mutex.ReleaseMutex();

      _device.DeviceApi.vkWaitForFences(_device.LogicalDevice, 1, &fence, VkBool32.True, UInt64.MaxValue);
      _device.DeviceApi.vkDestroyFence(_device.LogicalDevice, fence);
    }
  }

  public void BeginPostProcess(nint commandBuffer) {
    VkUtils.InsertMemoryBarrier2(
      _device,
      CurrentCommandBuffer,
      Swapchain.Images[_imageIndex],
      Swapchain.SurfaceFormat,
      VkImageLayout.PresentSrcKHR,
      VkImageLayout.ColorAttachmentOptimal,
      VkPipelineStageFlags2.None,
      VkPipelineStageFlags2.ColorAttachmentOutput,
      VkAccessFlags2.None,
      VkAccessFlags2.ColorAttachmentWrite
    );

    VkUtils.InsertMemoryBarrier2(
      _device,
      CurrentCommandBuffer,
      _depthStencil[_imageIndex].Image,
      DepthFormat.AsVkFormat(),
      VkImageLayout.DepthReadOnlyOptimal,
      VkImageLayout.DepthReadOnlyOptimal,
      VkPipelineStageFlags2.FragmentShader,
      VkPipelineStageFlags2.EarlyFragmentTests | VkPipelineStageFlags2.LateFragmentTests,
      VkAccessFlags2.ShaderRead,
      VkAccessFlags2.DepthStencilAttachmentRead
    );

    VkRenderingAttachmentInfo colorAttachment = new();
    colorAttachment.imageView = Swapchain.ImageViews[_imageIndex];
    colorAttachment.imageLayout = VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL;
    colorAttachment.loadOp = VK_ATTACHMENT_LOAD_OP_CLEAR;
    colorAttachment.storeOp = VK_ATTACHMENT_STORE_OP_STORE;
    colorAttachment.clearValue.color = new(0.137f, 0.137f, 0.137f, 1.0f);

    // VkRenderingAttachmentInfo depthStencilAttachment = new();
    // depthStencilAttachment.imageView = _depthStencil[_imageIndex].ImageView;
    // depthStencilAttachment.imageLayout = VK_IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL;
    // depthStencilAttachment.loadOp = VK_ATTACHMENT_LOAD_OP_CLEAR;
    // depthStencilAttachment.storeOp = VK_ATTACHMENT_STORE_OP_STORE;
    // depthStencilAttachment.clearValue.depthStencil = new(1.0f, 0);
    // depthStencilAttachment.resolveMode = VkResolveModeFlags.None;
    // depthStencilAttachment.resolveImageView = VkImageView.Null;
    // depthStencilAttachment.resolveImageLayout = VkImageLayout.Undefined;

    VkRenderingInfo renderingInfo = new();
    renderingInfo.renderArea = new(0, 0, Swapchain.Extent2D.Width, Swapchain.Extent2D.Height);
    renderingInfo.layerCount = 1;
    renderingInfo.colorAttachmentCount = 1;
    renderingInfo.pColorAttachments = &colorAttachment;
    // renderingInfo.pDepthAttachment = &depthStencilAttachment;
    renderingInfo.viewMask = 0;
    // renderingInfo.pStencilAttachment = &depthStencilAttachment;

    _device.DeviceApi.vkCmdBeginRendering(commandBuffer, &renderingInfo);

    VkViewport viewport = new() {
      x = 0.0f,
      y = 0.0f,
      width = Swapchain.Extent2D.Width,
      height = Swapchain.Extent2D.Height,
      minDepth = 0.0f,
      maxDepth = 1.0f
    };

    VkRect2D scissor = new(0, 0, Swapchain.Extent2D.Width, Swapchain.Extent2D.Height);
    _device.DeviceApi.vkCmdSetViewport(commandBuffer, 0, 1, &viewport);
    _device.DeviceApi.vkCmdSetScissor(commandBuffer, 0, 1, &scissor);
  }

  public void EndPostProcess(nint commandBuffer) {
    _device.DeviceApi.vkCmdEndRendering(commandBuffer);

    VkUtils.InsertMemoryBarrier2(
      _device,
      CurrentCommandBuffer, Swapchain.Images[_imageIndex],
      Swapchain.SurfaceFormat,
      VkImageLayout.ColorAttachmentOptimal,
      VkImageLayout.PresentSrcKHR,
      VkPipelineStageFlags2.ColorAttachmentOutput,
      VkPipelineStageFlags2.None,
      VkAccessFlags2.ColorAttachmentWrite,
      VkAccessFlags2.None
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

    // SceneColor: (UNDEFINED or SHADER_READ_ONLY) -> COLOR_ATTACHMENT
    VkUtils.InsertMemoryBarrier2(
      _device,
      CurrentCommandBuffer,
      _sceneColor[_imageIndex].Image,
      Swapchain.SurfaceFormat,
      VkImageLayout.ShaderReadOnlyOptimal,
      VkImageLayout.ColorAttachmentOptimal,
      VkPipelineStageFlags2.None,
      VkPipelineStageFlags2.ColorAttachmentOutput,
      VkAccessFlags2.None,
      VkAccessFlags2.ColorAttachmentWrite
    );

    // Depth: UNDEFINED -> DEPTH_ATTACHMENT
    VkUtils.InsertMemoryBarrier2(
      _device,
      CurrentCommandBuffer,
      _depthStencil[_imageIndex].Image,
      DepthFormat.AsVkFormat(),
      VkImageLayout.ShaderReadOnlyOptimal,
      VkImageLayout.DepthAttachmentOptimal,
      VkPipelineStageFlags2.None,
      VkPipelineStageFlags2.EarlyFragmentTests | VkPipelineStageFlags2.LateFragmentTests,
      VkAccessFlags2.None,
      VkAccessFlags2.DepthStencilAttachmentWrite
    );

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

    _device.DeviceApi.vkCmdBeginRendering(commandBuffer, &renderingInfo);

    VkViewport viewport = new() {
      x = 0.0f,
      y = 0.0f,
      width = Swapchain.Extent2D.Width,
      height = Swapchain.Extent2D.Height,
      minDepth = 0.0f,
      maxDepth = 1.0f
    };

    VkRect2D scissor = new(0, 0, Swapchain.Extent2D.Width, Swapchain.Extent2D.Height);
    _device.DeviceApi.vkCmdSetViewport(commandBuffer, 0, 1, &viewport);
    _device.DeviceApi.vkCmdSetScissor(commandBuffer, 0, 1, &scissor);
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

    _device.DeviceApi.vkCmdEndRendering(commandBuffer);

    VkUtils.InsertMemoryBarrier2(
      _device,
      CurrentCommandBuffer,
      _sceneColor[_imageIndex].Image,
      Swapchain.SurfaceFormat,
      VkImageLayout.ColorAttachmentOptimal,
      VkImageLayout.ShaderReadOnlyOptimal,
      VkPipelineStageFlags2.ColorAttachmentOutput,
      VkPipelineStageFlags2.FragmentShader,
      VkAccessFlags2.ColorAttachmentWrite,
      VkAccessFlags2.ShaderRead
    );

    // Depth (if sampling): DEPTH_ATTACHMENT -> DEPTH_READ_ONLY
    VkUtils.InsertMemoryBarrier2(
      _device,
      CurrentCommandBuffer, _depthStencil[_imageIndex].Image,
      DepthFormat.AsVkFormat(),
      VkImageLayout.DepthAttachmentOptimal,
      VkImageLayout.DepthReadOnlyOptimal,
      VkPipelineStageFlags2.EarlyFragmentTests | VkPipelineStageFlags2.LateFragmentTests,
      VkPipelineStageFlags2.FragmentShader,
      VkAccessFlags2.DepthStencilAttachmentWrite, VkAccessFlags2.ShaderRead
    );
  }

  public void CreateCommandBuffers(ulong commandPool, CommandBufferLevel level = CommandBufferLevel.Primary) {
    _commandBuffers = new VkCommandBuffer[Swapchain.Images.Length];

    VkCommandBufferAllocateInfo cmdBufAllocateInfo = new() {
      commandPool = commandPool,
      level = (VkCommandBufferLevel)level,
      commandBufferCount = (uint)_commandBuffers.Length
    };

    fixed (VkCommandBuffer* cmdBuffersPtr = _commandBuffers) {
      _device.DeviceApi.vkAllocateCommandBuffers(_device.LogicalDevice, &cmdBufAllocateInfo, cmdBuffersPtr).CheckResult();
    }
  }

  private void CreateSceneColor(int index) {
    var colorFormat = _swapchain.SurfaceFormat;
    VkImageCreateInfo imageCI = new();
    imageCI.imageType = VK_IMAGE_TYPE_2D;
    imageCI.format = colorFormat;
    imageCI.extent = new(Swapchain.Extent2D.Width, Swapchain.Extent2D.Height, 1);
    imageCI.mipLevels = 1;
    imageCI.arrayLayers = 1;
    imageCI.samples = VK_SAMPLE_COUNT_1_BIT;
    imageCI.tiling = VK_IMAGE_TILING_OPTIMAL;
    imageCI.usage = VK_IMAGE_USAGE_COLOR_ATTACHMENT_BIT | VK_IMAGE_USAGE_SAMPLED_BIT;

    _device.DeviceApi.vkCreateImage(_device.LogicalDevice, &imageCI, null, out _sceneColor[index].Image).CheckResult();

    VkMemoryRequirements memReqs;
    _device.DeviceApi.vkGetImageMemoryRequirements(_device.LogicalDevice, _sceneColor[index].Image, &memReqs);

    VkMemoryAllocateInfo alloc = new();
    alloc.allocationSize = memReqs.size;
    alloc.memoryTypeIndex = _device.FindMemoryType(memReqs.memoryTypeBits, MemoryProperty.DeviceLocal);
    _device.DeviceApi.vkAllocateMemory(_device.LogicalDevice, &alloc, null, out _sceneColor[index].ImageMemory).CheckResult();
    _device.DeviceApi.vkBindImageMemory(_device.LogicalDevice, _sceneColor[index].Image, _sceneColor[index].ImageMemory, 0).CheckResult();

    VkImageViewCreateInfo viewCI = new();
    viewCI.viewType = VK_IMAGE_VIEW_TYPE_2D;
    viewCI.image = _sceneColor[index].Image;
    viewCI.format = colorFormat;
    viewCI.subresourceRange.baseMipLevel = 0;
    viewCI.subresourceRange.levelCount = 1;
    viewCI.subresourceRange.baseArrayLayer = 0;
    viewCI.subresourceRange.layerCount = 1;
    viewCI.subresourceRange.aspectMask = VkUtils.AspectFor(colorFormat);

    _device.DeviceApi.vkCreateImageView(_device.LogicalDevice, &viewCI, null, out _sceneColor[index].ImageView).CheckResult();
  }

  public unsafe void Dispose() {
    _descriptorPool?.Dispose();
    _postProcessLayout?.Dispose();

    for (int i = 0; i < Swapchain.Images.Length; i++) {
      _device.DeviceApi.vkDestroyImageView(_device.LogicalDevice, _depthStencil[i].ImageView, null);
      _device.DeviceApi.vkDestroyImage(_device.LogicalDevice, _depthStencil[i].Image, null);
      _device.DeviceApi.vkFreeMemory(_device.LogicalDevice, _depthStencil[i].ImageMemory, null);

      _device.DeviceApi.vkDestroyImageView(_device.LogicalDevice, _sceneColor[i].ImageView, null);
      _device.DeviceApi.vkDestroyImage(_device.LogicalDevice, _sceneColor[i].Image, null);
      _device.DeviceApi.vkFreeMemory(_device.LogicalDevice, _sceneColor[i].ImageMemory, null);
    }

    foreach (var semaphore in _semaphores) {
      _device.DeviceApi.vkDestroySemaphore(_device.LogicalDevice, semaphore.PresentComplete, null);
      _device.DeviceApi.vkDestroySemaphore(_device.LogicalDevice, semaphore.RenderComplete, null);
    }

    _semaphores = [];

    foreach (var fence in _waitFences) {
      _device.DeviceApi.vkDestroyFence(_device.LogicalDevice, fence, null);
    }

    _device.DeviceApi.vkDestroySampler(_device.LogicalDevice, ImageSampler);
    _device.DeviceApi.vkDestroySampler(_device.LogicalDevice, DepthSampler);

    Swapchain?.Dispose();

    GC.SuppressFinalize(this);
  }

  public ulong PostProcessDecriptor => ImageDescriptors[ImageIndex];
  public ulong PreviousPostProcessDescriptor => ImageDescriptors[Swapchain.PreviousFrame];
  public nint CurrentCommandBuffer => _commandBuffers[_imageIndex];
  public int FrameIndex { get; private set; }
  public int ImageIndex => (int)_imageIndex;
  public float AspectRatio => Swapchain.ExtentAspectRatio();
  public NekoExtent2D Extent2D => Swapchain.Extent2D;
  public int MAX_FRAMES_IN_FLIGHT => Swapchain.Images.Length;
  public ISwapchain Swapchain => _swapchain;
  public CommandList CommandList { get; } = null!;
  public bool IsFrameInProgress { get; private set; } = false;
  public bool IsFrameStarted => IsFrameInProgress;
}