using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Vulkan;

public class VulkanDescriptorWriter {
  private readonly VulkanDevice _device;
  private readonly unsafe VulkanDescriptorSetLayout _setLayout;
  private readonly unsafe VulkanDescriptorPool _pool;
  private VkWriteDescriptorSet[] _writes = [];
  public VulkanDescriptorWriter(
    VulkanDevice device,
    VulkanDescriptorSetLayout setLayout,
    VulkanDescriptorPool pool
  ) {
    _device = device;
    _setLayout = setLayout;
    _pool = pool;
  }

  public unsafe VulkanDescriptorWriter(nint setLayout, nint pool) {
    _setLayout = null!;
    _pool = null!;
    throw new NotImplementedException();
    // _setLayout = &setLayout;
    // _pool = pool;
  }


  public unsafe VulkanDescriptorWriter WriteBuffer(uint binding, VkDescriptorBufferInfo* bufferInfo) {
    var bindingDescription = _setLayout.Bindings[binding];

    VkWriteDescriptorSet write = new() {
      descriptorType = bindingDescription.descriptorType,
      dstBinding = binding,
      pBufferInfo = bufferInfo,
      descriptorCount = 1
    };

    var tmp = _writes.ToList();
    tmp.Add(write);
    _writes = [.. tmp];
    return this;
  }

  public unsafe VulkanDescriptorWriter WriteImage(uint binding, VkDescriptorImageInfo* imageInfo) {
    var bindingDescription = _setLayout.Bindings[binding];

    VkWriteDescriptorSet write = new() {
      descriptorType = bindingDescription.descriptorType,
      dstBinding = binding,
      pImageInfo = imageInfo,
      descriptorCount = 1
    };

    var tmp = _writes.ToList();
    tmp.Add(write);
    _writes = [.. tmp];
    return this;
  }

  public unsafe VulkanDescriptorWriter WriteSampler(uint binding, VkSampler sampler) {
    var bindingDescription = _setLayout.Bindings[binding];

    VkDescriptorImageInfo samplerInfo = new() {
      sampler = sampler
    };

    VkWriteDescriptorSet imageWriteDescriptorSet = new() {
      descriptorType = bindingDescription.descriptorType,
      dstBinding = binding,
      descriptorCount = 1,
      pImageInfo = &samplerInfo
    };

    var tmp = _writes.ToList();
    tmp.Add(imageWriteDescriptorSet);
    _writes = [.. tmp];
    return this;
  }

  public unsafe bool Build(out VkDescriptorSet set) {
    bool success = _pool.AllocateDescriptor(_setLayout.GetDescriptorSetLayout(), out set);
    if (!success) {
      return false;
    }
    Overwrite(ref set);

    return true;
  }

  public unsafe void Overwrite(ref VkDescriptorSet set) {
    if (_writes.Length < 1) throw new ArgumentException("Writes length is less than 1");
    if (set.IsNull) throw new ArgumentException("Set is null"); ;

    for (uint i = 0; i < _writes.Length; i++) {
      _writes[i].dstSet = set;
    }

    _device.DeviceApi.vkUpdateDescriptorSets(_pool.Device.LogicalDevice, _writes);
  }

  public void Free() {
  }
}