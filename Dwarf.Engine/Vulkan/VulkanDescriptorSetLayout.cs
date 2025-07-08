using Dwarf.AbstractionLayer;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Vulkan;

public class VulkanDescriptorSetLayout : IDescriptorSetLayout {
  private readonly IDevice _device = null!;
  private readonly VkDescriptorSetLayout _descriptorSetLayout = VkDescriptorSetLayout.Null;
  public class Builder {
    private readonly IDevice _device = null!;
    private readonly Dictionary<uint, VkDescriptorSetLayoutBinding> _bindings = [];
    public Builder(IDevice device, Dictionary<uint, VkDescriptorSetLayoutBinding> bindings) {
      _device = device;
      _bindings = bindings;
    }

    public Builder(IDevice device) {
      _device = device;
    }

    public Builder AddBinding(
      uint binding,
      DescriptorType descriptorType,
      ShaderStageFlags shaderStageFlags,
      uint count = 1
    ) {
      VkDescriptorSetLayoutBinding layoutBinding = new() {
        binding = binding,
        descriptorType = (VkDescriptorType)descriptorType,
        descriptorCount = count,
        stageFlags = (VkShaderStageFlags)shaderStageFlags,
      };
      _bindings[binding] = layoutBinding;
      return this;
    }

    public VulkanDescriptorSetLayout Build() {
      return new VulkanDescriptorSetLayout(_device, _bindings);
    }

  }

  public unsafe VulkanDescriptorSetLayout(IDevice device, Dictionary<uint, VkDescriptorSetLayoutBinding> bindings) {
    _device = device;
    Bindings = bindings;

    uint bindingCount = (uint)bindings.Count;
    var setLayoutBindings = new VkDescriptorSetLayoutBinding[bindingCount];
    for (uint i = 0; i < bindingCount; i++) {
      setLayoutBindings[i] = bindings[i];
    }

    var bindingFlags = new VkDescriptorBindingFlags[bindingCount];
    for (int i = 0; i < bindingCount; i++) {
      bindingFlags[i] = VkDescriptorBindingFlags.PartiallyBound;
    }

    fixed (VkDescriptorSetLayoutBinding* bindingsPtr = setLayoutBindings)
    fixed (VkDescriptorBindingFlags* flagsPtr = bindingFlags) {
      var flagsCreateInfo = new VkDescriptorSetLayoutBindingFlagsCreateInfo {
        sType = VkStructureType.DescriptorSetLayoutBindingFlagsCreateInfo,
        bindingCount = bindingCount,
        pBindingFlags = flagsPtr
      };

      var layoutCreateInfo = new VkDescriptorSetLayoutCreateInfo {
        sType = VkStructureType.DescriptorSetLayoutCreateInfo,
        pNext = &flagsCreateInfo,
        bindingCount = bindingCount,
        pBindings = bindingsPtr
      };

      vkCreateDescriptorSetLayout(
        _device.LogicalDevice,
        &layoutCreateInfo,
        null,
        out _descriptorSetLayout
      ).CheckResult();
    }
  }

  // public unsafe VulkanDescriptorSetLayout(IDevice device, Dictionary<uint, VkDescriptorSetLayoutBinding> bindings) {
  //   _device = device;
  //   Bindings = bindings;

  //   VkDescriptorSetLayoutBinding[] setLayoutBindings = new VkDescriptorSetLayoutBinding[bindings.Count];
  //   for (uint i = 0; i < bindings.Count; i++) {
  //     setLayoutBindings[i] = bindings[i];
  //   }

  //   VkDescriptorSetLayoutCreateInfo descriptorSetLayoutInfo = new() {
  //     bindingCount = (uint)setLayoutBindings.Length,
  //   };
  //   fixed (VkDescriptorSetLayoutBinding* ptr = setLayoutBindings) {
  //     descriptorSetLayoutInfo.pBindings = ptr;
  //   }

  //   vkCreateDescriptorSetLayout(_device.LogicalDevice, &descriptorSetLayoutInfo, null, out _descriptorSetLayout).CheckResult();
  // }

  public VkDescriptorSetLayout GetDescriptorSetLayout() {
    return _descriptorSetLayout;
  }

  public ulong GetDescriptorSetLayoutPointer() {
    return _descriptorSetLayout.Handle;
  }

  public Dictionary<uint, VkDescriptorSetLayoutBinding> Bindings { get; } = new();

  public unsafe void Dispose() {
    vkDestroyDescriptorSetLayout(_device.LogicalDevice, _descriptorSetLayout);
  }
}