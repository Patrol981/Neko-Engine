using Neko.AbstractionLayer;
using Neko.Utils;
using Neko.Vulkan;

using StbImageSharp;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf;

public class CubeMapTexture : VulkanTexture {
  private readonly string[] _paths = [];
  private PackedTexture _cubemapPack;

  public CubeMapTexture(
    nint allocator,
    VulkanDevice device,
    int width,
    int height,
    string[] paths,
    string textureName = ""
  ) : base(allocator, device, width, height, textureName) {
    _paths = paths;

    var textures = ImageUtils.LoadTextures(_paths);
    _cubemapPack = ImageUtils.PackImage(textures);
    SetTextureData([.. _cubemapPack.ByteArray]);
  }

  public static new async Task<ImageResult> LoadDataFromPath(string path, int flip = 1) {
    return await TextureLoader.LoadDataFromPath(path, flip);
  }

  public new void SetTextureData(byte[] data) {
    var stagingBuffer = new DwarfBuffer(
      _allocator,
      _device,
      (ulong)_cubemapPack.Size,
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent
    );

    stagingBuffer.Map();
    unsafe {
      fixed (byte* textureDataPointer = data) {
        stagingBuffer.WriteToBuffer((nint)textureDataPointer, (ulong)_cubemapPack.Size);
      }
    }
    // stagingBuffer.WriteToBuffer(MemoryUtils.ToIntPtr(data), (ulong)_cubemapPack.Size);
    stagingBuffer.Unmap();

    ProcessTexture(stagingBuffer);

    stagingBuffer.Dispose();
  }

  public new void SetTextureData(nint dataPtr) {
    var stagingBuffer = new DwarfBuffer(
      _allocator,
      _device,
      (ulong)_cubemapPack.Size,
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent
    );

    /*
    var data = new byte[_cubemapPack.Size];
    Marshal.Copy(dataPtr, data, 0, data.Length);
    if (MemoryUtils.IsNull(data)) {
      Logger.Warn($"[Texture Bytes] Memory is null");
      return;
    }
    */

    stagingBuffer.Map();
    stagingBuffer.WriteToBuffer(dataPtr, (ulong)_cubemapPack.Size);
    // stagingBuffer.WriteToBuffer(MemoryUtils.ToIntPtr(data), (ulong)_cubemapPack.Size);
    stagingBuffer.Unmap();

    ProcessTexture(stagingBuffer);

    stagingBuffer.Dispose();
  }

  private void ProcessTexture(NekoBuffer stagingBuffer, VkImageCreateFlags createFlags = VkImageCreateFlags.None) {
    unsafe {
      if (_textureSampler.TextureImage.IsNotNull) {
        _device.WaitDevice();
        _device.DeviceApi.vkDestroyImage(_device.LogicalDevice, _textureSampler.TextureImage);
      }

      if (_textureSampler.TextureImageMemory.IsNotNull) {
        _device.WaitDevice();
        _device.DeviceApi.vkFreeMemory(_device.LogicalDevice, _textureSampler.TextureImageMemory);
      }
    }

    CreateImage(_device, (uint)_width, (uint)_height, VkFormat.R8G8B8A8Unorm, out _textureSampler.TextureImage, out _textureSampler.TextureImageMemory);
    HandleCubemap(stagingBuffer.GetBuffer(), VkFormat.R8G8B8A8Unorm, 1);
  }

  private static unsafe void CreateImage(
    VulkanDevice device,
    uint width,
    uint height,
    VkFormat format,
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

    // Cube faces count as array layers in Vulkan
    imageInfo.arrayLayers = 6;
    // This flag is required for cube map images
    imageInfo.flags = VkImageCreateFlags.CubeCompatible;

    device.DeviceApi.vkCreateImage(device.LogicalDevice, &imageInfo, null, out textureImage).CheckResult();
    device.DeviceApi.vkGetImageMemoryRequirements(device.LogicalDevice, textureImage, out VkMemoryRequirements memRequirements);

    VkMemoryAllocateInfo allocInfo = new();
    allocInfo.allocationSize = memRequirements.size;
    allocInfo.memoryTypeIndex = device.FindMemoryType(memRequirements.memoryTypeBits, MemoryProperty.DeviceLocal);

    device.DeviceApi.vkAllocateMemory(device.LogicalDevice, &allocInfo, null, out textureImageMemory).CheckResult();
    device.DeviceApi.vkBindImageMemory(device.LogicalDevice, textureImage, textureImageMemory, 0).CheckResult();
  }

  private unsafe void HandleCubemap(VkBuffer stagingBuffer, VkFormat format, uint mipLevels) {
    var copyCmd = _device.CreateCommandBuffer(VkCommandBufferLevel.Primary, true);

    var bufferCopyRegions = new List<VkBufferImageCopy>();
    uint offset = 0;

    for (uint face = 0; face < 6; face++) {
      for (uint level = 0; level < mipLevels; level++) {
        // Calculate offset into staging buffer for the current mip level and face
        // Handle Offsets

        uint mipWidth = (uint)System.Math.Max(1, _width >> (int)level);
        uint mipHeight = (uint)System.Math.Max(1, _height >> (int)level);
        uint mipSize = mipWidth * mipHeight * 4;

        var bufferCopyRegion = new VkBufferImageCopy();
        bufferCopyRegion.imageSubresource.aspectMask = VkImageAspectFlags.Color;
        bufferCopyRegion.imageSubresource.mipLevel = level;
        bufferCopyRegion.imageSubresource.baseArrayLayer = face;
        bufferCopyRegion.imageSubresource.layerCount = 1;
        bufferCopyRegion.imageExtent.width = (uint)(_width >> (int)level);
        bufferCopyRegion.imageExtent.height = (uint)(_height >> (int)level);
        bufferCopyRegion.imageExtent.depth = 1;
        bufferCopyRegion.bufferOffset = offset;
        bufferCopyRegions.Add(bufferCopyRegion);

        offset += (uint)_cubemapPack.Headers[face].Size;
      }
    }

    var subresourceRange = new VkImageSubresourceRange();
    subresourceRange.aspectMask = VkImageAspectFlags.Color;
    subresourceRange.baseMipLevel = 0;
    subresourceRange.levelCount = mipLevels;
    subresourceRange.layerCount = 6;

    VkUtils.SetImageLayout(
      _device,
      copyCmd,
      _textureSampler.TextureImage,
      VkImageLayout.Undefined,
      VkImageLayout.TransferDstOptimal,
      subresourceRange
    );
    fixed (VkBufferImageCopy* imageCopyPtr = bufferCopyRegions.ToArray()) {
      _device.DeviceApi.vkCmdCopyBufferToImage(
        copyCmd,
        stagingBuffer,
        _textureSampler.TextureImage,
        VkImageLayout.TransferDstOptimal,
        (uint)bufferCopyRegions.Count,
        imageCopyPtr
      );
    }

    VkUtils.SetImageLayout(
      _device,
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
    samplerInfo.maxLod = mipLevels;
    samplerInfo.borderColor = VkBorderColor.FloatOpaqueWhite;
    samplerInfo.maxAnisotropy = 1;
    if (_device.Features.samplerAnisotropy) {
      samplerInfo.maxAnisotropy = _device.Properties.properties.limits.maxSamplerAnisotropy;
      samplerInfo.anisotropyEnable = true;
    }
    _device.DeviceApi.vkCreateSampler(_device.LogicalDevice, &samplerInfo, null, out _textureSampler.ImageSampler).CheckResult();

    // create image view
    var viewInfo = new VkImageViewCreateInfo();
    // cubemap view type
    viewInfo.viewType = VkImageViewType.ImageCube;
    viewInfo.format = format;
    viewInfo.subresourceRange = new(VkImageAspectFlags.Color, 0, 1, 0, 1);
    // 6 array layers (faces)
    viewInfo.subresourceRange.layerCount = 6;
    // number of mip levels
    viewInfo.subresourceRange.levelCount = mipLevels;
    viewInfo.image = _textureSampler.TextureImage;
    _device.DeviceApi.vkCreateImageView(_device.LogicalDevice, &viewInfo, null, out _textureSampler.ImageView).CheckResult();
  }
}
