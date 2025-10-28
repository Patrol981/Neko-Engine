using System.Diagnostics;
using Neko.AbstractionLayer;
using Neko.Vulkan;
using glTFLoader.Schema;
using StbImageSharp;
using Vortice.Vulkan;

namespace Neko;

public class TextureLoader {
  public static async Task<ITexture> LoadFromPath(nint allocator, IDevice device, string path, int flip = 1) {
    switch (Application.Instance.CurrentAPI) {
      case RenderAPI.Vulkan:
        Debug.Assert(allocator != IntPtr.Zero);
        return await VulkanTexture.LoadFromPath(allocator, (VulkanDevice)device, path, flip);
      default:
        throw new NotImplementedException("Other apis are not currently supported");
    }
  }
  public static async Task<ImageResult> LoadDataFromPath(string path, int flip = 1) {
    return Application.Instance.CurrentAPI switch {
      RenderAPI.Vulkan => await VulkanTexture.LoadDataFromPath(path, flip),
      _ => throw new NotImplementedException("Other apis are not currently supported"),
    };
  }
  public static ImageResult LoadDataFromBytes(byte[] data, int flip = 1) {
    return Application.Instance.CurrentAPI switch {
      RenderAPI.Vulkan => VulkanTexture.LoadDataFromBytes(data, flip),
      _ => throw new NotImplementedException("Other apis are not currently supported"),
    };
  }

  public static ITexture LoadFromBytes(nint allocator, IDevice device, byte[] data, string textureName, int flip = 1) {
    switch (Application.Instance.CurrentAPI) {
      case RenderAPI.Vulkan:
        Debug.Assert(allocator != IntPtr.Zero);
        return VulkanTexture.LoadFromBytes(
          allocator,
          (VulkanDevice)device,
          data,
          textureName,
          flip
        );
      default:
        throw new NotImplementedException("Other apis are not currently supported");
    }
  }

  public static ITexture LoadFromGLTF(
    nint allocator,
    IDevice device,
    in Gltf gltf,
    in byte[] globalBuffer,
    Image gltfImage,
    string textureName,
    TextureSampler textureSampler,
    int flip
  ) {
    switch (Application.Instance.CurrentAPI) {
      case RenderAPI.Vulkan:
        Debug.Assert(allocator != IntPtr.Zero);
        return VulkanTexture.LoadFromGLTF(
          allocator,
          device,
          gltf,
          globalBuffer,
          gltfImage,
          textureName,
          textureSampler,
          flip
        );
      default:
        throw new NotImplementedException("Other apis are not currently supported");
    }
  }
}
