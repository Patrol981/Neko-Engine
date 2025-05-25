using Dwarf.AbstractionLayer;
using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;
namespace Dwarf;

public class TextureManager : IDisposable {
  private readonly IDevice _device;
  private readonly nint _allocator;

  public TextureManager(nint allocator, IDevice device) {
    _device = device;
    _allocator = allocator;
    PerSceneLoadedTextures = [];
    GlobalLoadedTextures = [];
    TextureArray = [];
  }

  public void AddRangeLocal(ITexture[] textures) {
    for (int i = 0; i < textures.Length; i++) {
      PerSceneLoadedTextures.Add(Guid.NewGuid(), textures[i]);
    }
  }

  public void AddRangeGlobal(ITexture[] textures) {
    for (int i = 0; i < textures.Length; i++) {
      GlobalLoadedTextures.Add(Guid.NewGuid(), textures[i]);
    }
  }

  public async Task<Task> AddTextureArray(string textureName, params string[] paths) {
    var baseData = await VulkanTextureArray.LoadDataFromPath(paths[0]);
    var id = Guid.NewGuid();
    var texture = new VulkanTextureArray(_allocator, (VulkanDevice)_device, baseData.Width, baseData.Height, paths, textureName);
    TextureArray.TryAdd(id, texture);

    return Task.CompletedTask;
  }

  public async Task<ITexture> AddTextureLocal(string texturePath, int flip = 1) {
    foreach (var tex in PerSceneLoadedTextures) {
      if (tex.Value.TextureName == texturePath) {
        Logger.Warn($"Texture [{texturePath}] is already loaded. Skipping current add call.");
        return tex.Value;
      }
    }
    var texture = await TextureLoader.LoadFromPath(_allocator, _device, texturePath, flip);
    PerSceneLoadedTextures.Add(Guid.NewGuid(), texture);
    return texture;
  }

  public async Task<ITexture> AddTextureGlobal(string texturePath, int flip = 1) {
    foreach (var tex in GlobalLoadedTextures) {
      if (tex.Value.TextureName == texturePath) {
        Logger.Warn($"Texture [{texturePath}] is already loaded. Skipping current add call.");
        return tex.Value;
      }
    }
    var texture = await TextureLoader.LoadFromPath(_allocator, _device, texturePath, flip);
    GlobalLoadedTextures.Add(Guid.NewGuid(), texture);
    return texture;
  }

  public Guid AddTextureLocal(ITexture texture) {
    foreach (var tex in PerSceneLoadedTextures) {
      if (tex.Value.TextureName == texture.TextureName) {
        Logger.Warn($"Texture [{texture.TextureName}] is already loaded. Skipping current add call.");
        return tex.Key;
      }
    }
    var guid = Guid.NewGuid();
    PerSceneLoadedTextures.Add(guid, texture);
    return guid;
  }

  public Guid AddTextureGlobal(ITexture texture) {
    foreach (var tex in GlobalLoadedTextures) {
      if (tex.Value.TextureName == texture.TextureName) {
        Logger.Warn($"Texture [{texture.TextureName}] is already loaded. Skipping current add call.");
        return tex.Key;
      }
    }
    var guid = Guid.NewGuid();
    GlobalLoadedTextures.Add(guid, texture);
    return guid;
  }

  public bool TextureExistsLocal(ITexture texture) {
    foreach (var tex in PerSceneLoadedTextures) {
      if (tex.Value.TextureName == texture.TextureName) {
        return true;
      }
    }

    return false;
  }

  public bool TextureExistsLocal(string textureName) {
    foreach (var tex in PerSceneLoadedTextures) {
      if (tex.Value.TextureName == textureName) {
        return true;
      }
    }

    return false;
  }

  public bool TextureExistsGlobal(ITexture texture) {
    foreach (var tex in GlobalLoadedTextures) {
      if (tex.Value.TextureName == texture.TextureName) {
        return true;
      }
    }

    return false;
  }

  public bool TextureExistsGlobal(string textureName) {
    foreach (var tex in GlobalLoadedTextures) {
      if (tex.Value.TextureName == textureName) {
        return true;
      }
    }

    return false;
  }

  public static async Task<ITexture[]> AddTextures(nint allocator, IDevice device, string[] paths, int flip = 1) {
    var textures = new ITexture[paths.Length];
    for (int i = 0; i < textures.Length; i++) {
      textures[i] = await TextureLoader.LoadFromPath(allocator, device, paths[i], flip);
    }
    return textures;
  }

  public static ITexture[] AddTextures(nint allocator, IDevice device, List<byte[]> bytes, string[] nameTags) {
    var textures = new ITexture[bytes.Count];
    for (int i = 0; i < bytes.Count; i++) {
      var imgData = TextureLoader.LoadDataFromBytes(bytes[i]);
      _ = Application.Instance.CurrentAPI switch {
        RenderAPI.Vulkan => textures[i] = new VulkanTexture(allocator, (VulkanDevice)device, imgData.Width, imgData.Height, nameTags[i]),
        _ => throw new NotImplementedException(),
      };
      textures[i].SetTextureData(imgData.Data);
    }
    return textures;
  }

  public void RemoveTextureLocal(Guid key) {
    PerSceneLoadedTextures[key].Dispose();
    PerSceneLoadedTextures.Remove(key);
  }

  public void RemoveTextureGlobal(Guid key) {
    GlobalLoadedTextures[key].Dispose();
    GlobalLoadedTextures.Remove(key);
  }

  public void RemoveTextureArray(Guid key) {
    TextureArray[key].Dispose();
    TextureArray.Remove(key);
  }

  public ITexture GetTextureLocal(Guid key) {
    return PerSceneLoadedTextures.GetValueOrDefault(key) ?? null!;
  }

  public ITexture GetTextureGlobal(Guid key) {
    return GlobalLoadedTextures.GetValueOrDefault(key) ?? null!;
  }

  public (Guid guid, ITexture texture) GetTextureLocal(int textureIndex) {
    var target = PerSceneLoadedTextures.Single(x => x.Value.TextureIndex == textureIndex);
    return (target.Key, target.Value);
  }

  public (Guid guid, ITexture texture) GetTextureGlobal(int textureIndex) {
    var target = GlobalLoadedTextures.Single(x => x.Value.TextureIndex == textureIndex);
    return (target.Key, target.Value);
  }

  public Guid GetTextureIdLocal(string textureName) {
    foreach (var tex in PerSceneLoadedTextures) {
      if (tex.Value.TextureName == textureName) {
        return tex.Key;
      }
    }
    return Guid.Empty;
  }

  public Guid GetTextureIdGlobal(string textureName) {
    foreach (var tex in GlobalLoadedTextures) {
      if (tex.Value.TextureName == textureName) {
        return tex.Key;
      }
    }
    return Guid.Empty;
  }

  public Dictionary<Guid, ITexture> PerSceneLoadedTextures { get; }
  public Dictionary<Guid, ITexture> GlobalLoadedTextures { get; }
  public Dictionary<Guid, VulkanTextureArray> TextureArray { get; }

  public void DisposeLocal() {
    foreach (var tex in PerSceneLoadedTextures) {
      RemoveTextureLocal(tex.Key);
    }
  }

  public void DisposeGlobal() {
    foreach (var tex in GlobalLoadedTextures) {
      RemoveTextureGlobal(tex.Key);
    }
  }

  public void Dispose() {
    DisposeLocal();
    DisposeGlobal();
    foreach (var tex in TextureArray) {
      RemoveTextureArray(tex.Key);
    }
  }
}