using Dwarf.AbstractionLayer;
using Dwarf.Extensions.Lists;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Vulkan;

public class VulkanDescriptorPool : IDescriptorPool {
  private VkDescriptorPool _descriptorPool;
  public class Builder {
    private readonly IDevice _device;
    private VkDescriptorPoolSize[] _poolSizes = [];
    private uint _maxSets = 1000;
    private VkDescriptorPoolCreateFlags _poolFlags = 0;

    public Builder(IDevice device, uint maxSets, VkDescriptorPoolCreateFlags poolFlags, VkDescriptorPoolSize[] poolSizes) {
      this._device = device;
      this._maxSets = maxSets;
      this._poolSizes = poolSizes;
      this._poolFlags = poolFlags;
    }

    public Builder(IDevice device) {
      this._device = device;
    }

    public Builder AddPoolSize(DescriptorType descriptorType, uint count) {
      VkDescriptorPoolSize poolSize = new() {
        descriptorCount = count,
        type = (VkDescriptorType)descriptorType
      };
      var tmpList = _poolSizes.ToList();
      tmpList.Add(poolSize);
      _poolSizes = [.. tmpList];
      return this;
    }

    public Builder SetPoolFlags(DescriptorPoolCreateFlags flags) {
      _poolFlags = (VkDescriptorPoolCreateFlags)flags;
      return this;
    }

    public Builder SetMaxSets(uint count) {
      _maxSets = count;
      return this;
    }

    public VulkanDescriptorPool Build() {
      return new VulkanDescriptorPool(_device, _maxSets, _poolFlags, _poolSizes);
    }
  }

  public unsafe VulkanDescriptorPool(
    IDevice device,
    uint maxSets,
    VkDescriptorPoolCreateFlags poolFlags,
    VkDescriptorPoolSize[] poolSizes
  ) {
    Device = device;

    VkDescriptorPoolCreateInfo descriptorPoolInfo = new();
    descriptorPoolInfo.poolSizeCount = (uint)poolSizes.Length;
    fixed (VkDescriptorPoolSize* ptr = poolSizes) {
      descriptorPoolInfo.pPoolSizes = ptr;
    }
    descriptorPoolInfo.maxSets = maxSets;
    descriptorPoolInfo.flags = poolFlags;

    vkCreateDescriptorPool(Device.LogicalDevice, &descriptorPoolInfo, null, out _descriptorPool).CheckResult();
  }

  public unsafe bool AllocateDescriptor(VkDescriptorSetLayout descriptorSetLayout, out VkDescriptorSet descriptorSet) {
    VkDescriptorSetAllocateInfo allocInfo = new() {
      descriptorPool = _descriptorPool,
      pSetLayouts = &descriptorSetLayout,
      descriptorSetCount = 1
    };

    fixed (VkDescriptorSet* ptr = &descriptorSet) {
      var result = vkAllocateDescriptorSets(Device.LogicalDevice, &allocInfo, ptr);
      return result == VkResult.Success;
    }
  }

  public unsafe void FreeDescriptors(VkDescriptorSet[] descriptorSets) {
    fixed (VkDescriptorSet* ptr = descriptorSets) {
      vkFreeDescriptorSets(Device.LogicalDevice, _descriptorPool, (uint)descriptorSets.Length, ptr).CheckResult();
    }
  }

  public unsafe void FreeDescriptors(PublicList<VkDescriptorSet> descriptorSets) {
    fixed (VkDescriptorSet* ptr = descriptorSets.GetData()) {
      vkFreeDescriptorSets(Device.LogicalDevice, _descriptorPool, (uint)descriptorSets.Size, ptr).CheckResult();
    }
  }

  public unsafe void FreeDescriptors(PublicList<PublicList<VkDescriptorSet>> descriptorSets) {
    for (int i = 0; i < descriptorSets.Size; i++) {
      fixed (VkDescriptorSet* ptr = descriptorSets.GetAt(i).GetData()) {
        vkFreeDescriptorSets(Device.LogicalDevice, _descriptorPool, (uint)descriptorSets.GetAt(i).Size, ptr).CheckResult();
      }
    }
  }

  public unsafe void ResetPool() {
    vkResetDescriptorPool(Device.LogicalDevice, _descriptorPool, 0).CheckResult();
  }

  public VkDescriptorPool GetVkDescriptorPool() {
    return _descriptorPool;
  }

  public ulong GetHandle() {
    return _descriptorPool.Handle;
  }

  public IDevice Device { get; }

  public unsafe void Dispose() {
    vkDestroyDescriptorPool(Device.LogicalDevice, _descriptorPool);
  }
}