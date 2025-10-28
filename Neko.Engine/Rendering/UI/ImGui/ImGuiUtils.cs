using Neko.AbstractionLayer;
using Neko.Utils;

namespace Neko.Rendering.UI;

public partial class ImGuiController {
  private readonly List<string> _addedTextures = new List<string>();
  private readonly Dictionary<IntPtr, ITexture> _userTextures = new();
  private int _lastId = 100;

  public unsafe IntPtr GetOrCreateImGuiBinding(ITexture texture) {
    return Application.Instance.CurrentAPI switch {
      RenderAPI.Vulkan => VkGetOrCreateImGuiBinding((VulkanTexture)texture),
      _ => throw new NotImplementedException("Other apis are not currently supported"),
    };
  }

  private unsafe IntPtr VkGetOrCreateImGuiBinding(VulkanTexture texture) {
    if (texture == null) return IntPtr.Zero;
    if (!_addedTextures.Contains(texture.TextureName)) {
      texture.AddDescriptor(_systemSetLayout, _systemDescriptorPool);
      var descriptorSet = texture.VkTextureDescriptor.Handle;
      var ptr = MemoryUtils.ToIntPtr(descriptorSet);

      _userTextures.TryAdd(ptr, texture);
      _addedTextures.Add(texture.TextureName);
      return ptr;
    } else {
      var target = _userTextures.Where(x => x.Value.TextureName == texture.TextureName).FirstOrDefault();
      return target.Key;
    }
  }

  public ITexture[] StoredTextures => [.. _userTextures.Values];

  private IntPtr GetNextImGuiBinding() {
    int newId = _lastId++;
    return newId;
  }
}
