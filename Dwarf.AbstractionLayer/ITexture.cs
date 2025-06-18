using Vortice.Vulkan;

namespace Dwarf.AbstractionLayer;

public enum IFilter {
  /// <unmanaged>VK_FILTER_NEAREST</unmanaged>
  Nearest = 0,
  /// <unmanaged>VK_FILTER_LINEAR</unmanaged>
  Linear = 1,
  /// <unmanaged>VK_FILTER_CUBIC_EXT</unmanaged>
  CubicEXT = 1000015000,
  /// <unmanaged>VK_FILTER_CUBIC_IMG</unmanaged>
  CubicIMG = CubicEXT,
}

public enum ISamplerAddressMode {
  /// <unmanaged>VK_SAMPLER_ADDRESS_MODE_REPEAT</unmanaged>
  Repeat = 0,
  /// <unmanaged>VK_SAMPLER_ADDRESS_MODE_MIRRORED_REPEAT</unmanaged>
  MirroredRepeat = 1,
  /// <unmanaged>VK_SAMPLER_ADDRESS_MODE_CLAMP_TO_EDGE</unmanaged>
  ClampToEdge = 2,
  /// <unmanaged>VK_SAMPLER_ADDRESS_MODE_CLAMP_TO_BORDER</unmanaged>
  ClampToBorder = 3,
  /// <unmanaged>VK_SAMPLER_ADDRESS_MODE_MIRROR_CLAMP_TO_EDGE</unmanaged>
  MirrorCl
}

public struct TextureSampler {
  public IFilter MagFilter;
  public IFilter MinFilter;
  public ISamplerAddressMode AddressModeU;
  public ISamplerAddressMode AddressModeV;
  public ISamplerAddressMode AddressModeW;
};

public interface ITexture : IDisposable {
  public string TextureName { get; }

  public void SetTextureData(nint dataPtr);
  public void SetTextureData(byte[] data);

  // public Task<ITexture> LoadFromPath(IDevice device, string path, int flip);
  // public Task<ImageResult> LoadDataFromPath(string path, int flip = 1);
  // public ImageResult LoadDataFromBytes(byte[] data, int flip = 1);

  public ulong Sampler { get; }
  public ulong ImageView { get; }
  public int TextureIndex { get; set; }
  public int TextureManagerIndex { get; set; }
  public ulong TextureImage { get; }
  public ulong TextureDescriptor { get; }
  public void BuildDescriptor(IDescriptorSetLayout descriptorSetLayout, IDescriptorPool descriptorPool, uint dstBindingStartIndex = 0);
  public void AddDescriptor(IDescriptorSetLayout descriptorSetLayout, IDescriptorPool descriptorPool);
  public int Width { get; }
  public int Height { get; }
  public int Size { get; }
  public byte[] TextureData { get; }
}
