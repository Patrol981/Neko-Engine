using Dwarf.AbstractionLayer;
using Dwarf.Utils;
using Dwarf.Vulkan;

using StbImageSharp;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf;

public class VulkanTextureArray : VulkanTexture {
  private readonly string[] _paths = [];
  private PackedTexture _textures;

  public VulkanTextureArray(
    nint allocator,
    VulkanDevice device,
    int width,
    int height,
    string[] paths,
    string textureName = ""
  ) : base(allocator, device, width, height, textureName) {
    _paths = paths;

    var textures = ImageUtils.LoadTextures(_paths);
    _textures = ImageUtils.PackImage(textures);
    SetTextureData([.. _textures.ByteArray]);
  }

  public static new async Task<ImageResult> LoadDataFromPath(string path, int flip = 1) {
    return await TextureLoader.LoadDataFromPath(path, flip);
  }

  public new void SetTextureData(byte[] data) {
    var stagingBuffer = new DwarfBuffer(
      _allocator,
      _device,
      (ulong)_textures.Size,
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent
    );

    stagingBuffer.Map();
    unsafe {
      fixed (byte* dataPtr = data) {
        stagingBuffer.WriteToBuffer((nint)dataPtr, (ulong)_textures.Size);
      }
    }
    // stagingBuffer.WriteToBuffer(MemoryUtils.ToIntPtr(data), (ulong)_textures.Size);
    stagingBuffer.Unmap();

    ProcessTexture(stagingBuffer);

    stagingBuffer.Dispose();
  }

  private void ProcessTexture(DwarfBuffer stagingBuffer, VkImageCreateFlags createFlags = VkImageCreateFlags.None) {
    unsafe {
      if (_textureSampler.TextureImage.IsNotNull) {
        _device.WaitDevice();
        vkDestroyImage(_device.LogicalDevice, _textureSampler.TextureImage);
      }

      if (_textureSampler.TextureImageMemory.IsNotNull) {
        _device.WaitDevice();
        vkFreeMemory(_device.LogicalDevice, _textureSampler.TextureImageMemory);
      }
    }

    CreateImage(_device, (uint)_width, (uint)_height, VkFormat.R8G8B8A8Unorm, (uint)_textures.Headers.Length, out _textureSampler.TextureImage, out _textureSampler.TextureImageMemory);
    HandleTextureArray(stagingBuffer.GetBuffer(), VkFormat.R8G8B8A8Unorm);
  }

  private static unsafe void CreateImage(
    VulkanDevice device,
    uint width,
    uint height,
    VkFormat format,
    uint layerCount,
    out VkImage textureImage,
    out VkDeviceMemory textureImageMemory
  ) {
    var imageInfo = new VkImageCreateInfo();
    imageInfo.imageType = VkImageType.Image2D;
    imageInfo.format = format;
    imageInfo.mipLevels = 1;
    imageInfo.samples = VkSampleCountFlags.Count1;
    imageInfo.tiling = VkImageTiling.Optimal;
    imageInfo.sharingMode = VkSharingMode.Exclusive;
    imageInfo.initialLayout = VkImageLayout.Undefined;
    imageInfo.extent.width = width;
    imageInfo.extent.height = height;
    imageInfo.usage = VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled;
    imageInfo.extent.depth = 1;
    imageInfo.arrayLayers = layerCount;
    // imageInfo.flags = VkImageCreateFlags.Array2DCompatible;

    vkCreateImage(device.LogicalDevice, &imageInfo, null, out textureImage).CheckResult();
    vkGetImageMemoryRequirements(device.LogicalDevice, textureImage, out VkMemoryRequirements memRequirements);

    VkMemoryAllocateInfo allocInfo = new();
    allocInfo.allocationSize = memRequirements.size;
    allocInfo.memoryTypeIndex = device.FindMemoryType(memRequirements.memoryTypeBits, MemoryProperty.DeviceLocal);

    vkAllocateMemory(device.LogicalDevice, &allocInfo, null, out textureImageMemory).CheckResult();
    vkBindImageMemory(device.LogicalDevice, textureImage, textureImageMemory, 0).CheckResult();
  }

  private unsafe void HandleTextureArray(VkBuffer stagingBuffer, VkFormat format) {
    var copyCmd = _device.CreateCommandBuffer(VkCommandBufferLevel.Primary, true);

    var bufferCopyRegions = new List<VkBufferImageCopy>();
    uint offset = 0;

    for (uint layer = 0; layer < _textures.Headers.Length; layer++) {
      var bufferCopyRegion = new VkBufferImageCopy();
      bufferCopyRegion.imageSubresource.aspectMask = VkImageAspectFlags.Color;
      bufferCopyRegion.imageSubresource.mipLevel = 0;
      bufferCopyRegion.imageSubresource.baseArrayLayer = layer;
      bufferCopyRegion.imageSubresource.layerCount = 1;
      bufferCopyRegion.imageExtent.width = (uint)_textures.Headers[layer].Width;
      bufferCopyRegion.imageExtent.height = (uint)_textures.Headers[layer].Height;
      bufferCopyRegion.imageExtent.depth = 1;
      bufferCopyRegion.bufferOffset = offset;
      bufferCopyRegions.Add(bufferCopyRegion);

      offset += (uint)_textures.Headers[layer].Size;
    }

    var subresourceRange = new VkImageSubresourceRange();
    subresourceRange.aspectMask = VkImageAspectFlags.Color;
    subresourceRange.baseMipLevel = 0;
    subresourceRange.levelCount = 1;
    subresourceRange.layerCount = (uint)_textures.Headers.Length;

    VkUtils.SetImageLayout(
      copyCmd,
      _textureSampler.TextureImage,
      VkImageLayout.Undefined,
      VkImageLayout.TransferDstOptimal,
      subresourceRange
    );

    fixed (VkBufferImageCopy* imageCopyPtr = bufferCopyRegions.ToArray()) {
      vkCmdCopyBufferToImage(
        copyCmd,
        stagingBuffer,
        _textureSampler.TextureImage,
        VkImageLayout.TransferDstOptimal,
        (uint)bufferCopyRegions.Count,
        imageCopyPtr
      );
    }

    VkUtils.SetImageLayout(
      copyCmd,
      _textureSampler.TextureImage,
      VkImageLayout.TransferDstOptimal,
      VkImageLayout.ShaderReadOnlyOptimal,
      subresourceRange
    );

    _device.FlushCommandBuffer(copyCmd, _device.GraphicsQueue, true);

    // create sampler
    var samplerInfo = new VkSamplerCreateInfo();
    samplerInfo.magFilter = VkFilter.Linear;
    samplerInfo.minFilter = VkFilter.Linear;
    samplerInfo.mipmapMode = VkSamplerMipmapMode.Linear;
    samplerInfo.addressModeU = VkSamplerAddressMode.ClampToEdge;
    samplerInfo.addressModeV = samplerInfo.addressModeU;
    samplerInfo.addressModeW = samplerInfo.addressModeU;
    samplerInfo.mipLodBias = 0;
    samplerInfo.compareOp = VkCompareOp.Never;
    samplerInfo.minLod = 0;
    samplerInfo.maxLod = 0;
    samplerInfo.borderColor = VkBorderColor.FloatOpaqueWhite;
    samplerInfo.maxAnisotropy = 1;
    if (_device.Features.samplerAnisotropy) {
      samplerInfo.maxAnisotropy = _device.Properties.limits.maxSamplerAnisotropy;
      samplerInfo.anisotropyEnable = true;
    }
    vkCreateSampler(_device.LogicalDevice, &samplerInfo, null, out _textureSampler.ImageSampler).CheckResult();

    var viewInfo = new VkImageViewCreateInfo();
    viewInfo.viewType = VkImageViewType.Image2DArray;
    viewInfo.format = format;
    viewInfo.subresourceRange = new(VkImageAspectFlags.Color, 0, 1, 0, 1);
    viewInfo.subresourceRange.layerCount = (uint)_textures.Headers.Length;
    viewInfo.subresourceRange.levelCount = 1;
    viewInfo.image = _textureSampler.TextureImage;
    vkCreateImageView(_device.LogicalDevice, &viewInfo, null, out _textureSampler.ImageView).CheckResult();
  }
}
