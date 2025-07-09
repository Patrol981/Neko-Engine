using Dwarf.AbstractionLayer;
using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Dwarf;

public class TextureManager : IDisposable {
  private readonly IDevice _device;
  private readonly nint _allocator;

  private int _nextLocalIndex = 0;
  private readonly Stack<int> _freeLocalSlots = new();

  private int _nextGlobalIndex = 0;
  private readonly Stack<int> _freeGlobalSlots = new();

  public IDescriptorPool ManagerPool { get; private set; } = null!;
  public IDescriptorSetLayout AllTexturesSetLayout { get; private set; } = null!;

  public Dictionary<Guid, ITexture> PerSceneLoadedTextures { get; }
  public Dictionary<Guid, ITexture> GlobalLoadedTextures { get; }
  public Dictionary<Guid, VulkanTextureArray> TextureArray { get; }

  private ulong _allTexturesSampler;
  private object[] _allTexturesInfos = new object[CommonConstants.MAX_TEXTURE];
  public ulong AllTexturesDescriptor { get; private set; }

  public TextureManager(nint allocator, IDevice device) {
    _device = device;
    _allocator = allocator;
    PerSceneLoadedTextures = [];
    GlobalLoadedTextures = [];
    TextureArray = [];

    Init();
  }

  private int AllocateLocalIndex() {
    return _freeLocalSlots.Count > 0
      ? _freeLocalSlots.Pop()
      : _nextLocalIndex++;
  }

  private void FreeLocalIndex(int idx) {
    _freeLocalSlots.Push(idx);
  }

  private int AllocateGlobalIndex() {
    return _freeGlobalSlots.Count > 0
      ? _freeGlobalSlots.Pop()
      : _nextGlobalIndex++;
  }

  private void FreeGlobalIndex(int idx) {
    _freeGlobalSlots.Push(idx);
  }

  public void AddRangeLocal(ITexture[] textures) {
    for (int i = 0; i < textures.Length; i++) {
      textures[i].TextureManagerIndex = AllocateLocalIndex();
      PerSceneLoadedTextures.Add(Guid.NewGuid(), textures[i]);
    }
    RebuildDescriptors();
  }

  public void AddRangeGlobal(ITexture[] textures) {
    for (int i = 0; i < textures.Length; i++) {
      textures[i].TextureManagerIndex = AllocateGlobalIndex();
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
        // Logger.Warn($"Texture [{texturePath}] is already loaded. Skipping current add call.");
        return tex.Value;
      }
    }
    Application.Mutex.WaitOne();
    var texture = await TextureLoader.LoadFromPath(_allocator, _device, texturePath, flip);
    texture.TextureManagerIndex = AllocateLocalIndex();
    PerSceneLoadedTextures.Add(Guid.NewGuid(), texture);
    RebuildDescriptors();
    Application.Mutex.ReleaseMutex();
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
    texture.TextureManagerIndex = AllocateGlobalIndex();
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
    texture.TextureManagerIndex = AllocateLocalIndex();
    PerSceneLoadedTextures.Add(guid, texture);
    RebuildDescriptors();
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
    texture.TextureManagerIndex = AllocateGlobalIndex();
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
    FreeLocalIndex(PerSceneLoadedTextures[key].TextureManagerIndex);
    PerSceneLoadedTextures[key].Dispose();
    PerSceneLoadedTextures.Remove(key);
  }

  public void RemoveTextureGlobal(Guid key) {
    FreeGlobalIndex(GlobalLoadedTextures[key].TextureManagerIndex);
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

  private void RebuildDescriptors() {
    switch (_device.RenderAPI) {
      case RenderAPI.Vulkan:
        RebuildVkDescriptors();
        break;
      default:
        break;
    }
  }

  private void Init() {
    switch (_device.RenderAPI) {
      case RenderAPI.Vulkan:
        VkInit();
        break;
      default:
        break;
    }
  }

  private void VkInit() {
    ManagerPool = new VulkanDescriptorPool.Builder(_device)
      .SetMaxSets(CommonConstants.MAX_SETS)
      .AddPoolSize(DescriptorType.SampledImage, CommonConstants.MAX_SETS)
      .AddPoolSize(DescriptorType.Sampler, CommonConstants.MAX_SETS)
      .SetPoolFlags(DescriptorPoolCreateFlags.UpdateAfterBind)
      .Build();

    AllTexturesSetLayout = new VulkanDescriptorSetLayout.Builder(_device)
      .AddBinding(0, DescriptorType.SampledImage, ShaderStageFlags.Fragment, CommonConstants.MAX_TEXTURE)
      .AddBinding(1, DescriptorType.Sampler, ShaderStageFlags.Fragment)
      .Build();
  }

  private unsafe void RebuildVkDescriptors() {
    // if (AllTexturesDescriptor != 0) {
    //   vkFreeDescriptorSets(_device.LogicalDevice, ManagerPool.GetHandle(), AllTexturesDescriptor);
    // }
    Array.Clear(_allTexturesInfos);

    if (_allTexturesSampler == 0) {
      VkPhysicalDeviceProperties properties = new();
      vkGetPhysicalDeviceProperties(_device.PhysicalDevice, &properties);

      var samplerCreateInfo = new VkSamplerCreateInfo() {
        magFilter = VkFilter.Nearest,
        minFilter = VkFilter.Nearest,
        addressModeU = VkSamplerAddressMode.Repeat,
        addressModeV = VkSamplerAddressMode.Repeat,
        addressModeW = VkSamplerAddressMode.Repeat,
        anisotropyEnable = true,
        maxAnisotropy = properties.limits.maxSamplerAnisotropy,
        borderColor = VkBorderColor.IntOpaqueBlack,
        unnormalizedCoordinates = false,
        compareEnable = false,
        compareOp = VkCompareOp.Always,
        mipmapMode = VkSamplerMipmapMode.Nearest
      };
      vkCreateSampler(_device.LogicalDevice, &samplerCreateInfo, null, out var sampler).CheckResult();
      _allTexturesSampler = sampler.Handle;
    }

    var textureValues = PerSceneLoadedTextures.Values.ToArray();
    for (int i = 0; i < textureValues.Length; i++) {
      _allTexturesInfos[i] = new VkDescriptorImageInfo() {
        sampler = 0,
        imageLayout = VkImageLayout.ShaderReadOnlyOptimal,
        imageView = textureValues[i].ImageView
      };
    }

    VkDescriptorSet descriptorSet = new();
    var allocInfo = new VkDescriptorSetAllocateInfo {
      descriptorPool = ManagerPool.GetHandle(),
      descriptorSetCount = 1
    };
    var setLayout = AllTexturesSetLayout.GetDescriptorSetLayoutPointer();
    allocInfo.pSetLayouts = (VkDescriptorSetLayout*)&setLayout;
    vkAllocateDescriptorSets(_device.LogicalDevice, &allocInfo, &descriptorSet);

    var samplerInfo = new VkDescriptorImageInfo() {
      sampler = _allTexturesSampler
    };

    // var infos = _allTexturesInfos
    // .Where(x => x != null)
    // .Select(x => {
    //   return (VkDescriptorImageInfo)x;
    // }).ToArray();

    var textureCount = textureValues.Length;
    var infos = new VkDescriptorImageInfo[textureCount];
    for (int i = 0; i < textureCount; i++) {
      infos[i] = new VkDescriptorImageInfo {
        sampler = 0,
        imageView = textureValues[i].ImageView,
        imageLayout = VkImageLayout.ShaderReadOnlyOptimal
      };
    }

    fixed (VkDescriptorImageInfo* pImageInfos = infos) {
      VkWriteDescriptorSet* writes = stackalloc VkWriteDescriptorSet[2];
      writes[0] = new VkWriteDescriptorSet() {
        sType = VkStructureType.WriteDescriptorSet,
        dstBinding = 0,
        dstArrayElement = 0,
        descriptorType = VkDescriptorType.SampledImage,
        descriptorCount = (uint)textureCount,
        pBufferInfo = null,
        dstSet = descriptorSet,
        pImageInfo = pImageInfos
      };
      writes[1] = new VkWriteDescriptorSet() {
        sType = VkStructureType.WriteDescriptorSet,
        dstBinding = 1,
        dstArrayElement = 0,
        descriptorType = VkDescriptorType.Sampler,
        descriptorCount = 1,
        dstSet = descriptorSet,
        pBufferInfo = null,
        pImageInfo = &samplerInfo
      };

      vkUpdateDescriptorSets(_device.LogicalDevice, 2, writes, 0, null);
      AllTexturesDescriptor = descriptorSet;
    }
  }

  public void DisposeLocal() {
    foreach (var tex in PerSceneLoadedTextures) {
      RemoveTextureLocal(tex.Key);
    }
    _nextLocalIndex = 0;
    _freeLocalSlots.Clear();
  }

  public void DisposeGlobal() {
    foreach (var tex in GlobalLoadedTextures) {
      RemoveTextureGlobal(tex.Key);
    }
    _nextGlobalIndex = 0;
    _freeGlobalSlots.Clear();
  }

  private unsafe void VkDispose() {
    Array.Clear(_allTexturesInfos);
    vkDestroySampler(_device.LogicalDevice, _allTexturesSampler);
    vkFreeDescriptorSets(_device.LogicalDevice, ManagerPool.GetHandle(), AllTexturesDescriptor);
    vkDestroyDescriptorPool(_device.LogicalDevice, ManagerPool.GetHandle());
    vkDestroyDescriptorSetLayout(_device.LogicalDevice, AllTexturesSetLayout.GetDescriptorSetLayoutPointer());
  }

  public void Dispose() {
    DisposeLocal();
    DisposeGlobal();
    foreach (var tex in TextureArray) {
      RemoveTextureArray(tex.Key);
    }

    switch (_device.RenderAPI) {
      case RenderAPI.Vulkan:
        VkDispose();
        break;
      default:
        break;
    }
  }
}