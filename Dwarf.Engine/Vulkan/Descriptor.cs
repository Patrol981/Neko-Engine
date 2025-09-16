using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Vulkan;

public static class Descriptor {
  public static unsafe void BindDescriptorSet(
    VulkanDevice device,
    VkDescriptorSet descriptorSet,
    FrameInfo frameInfo,
    VkPipelineLayout pipelineLayout,
    uint firstSet,
    uint setCount
  ) {
    device.DeviceApi.vkCmdBindDescriptorSets(
      frameInfo.CommandBuffer,
      VkPipelineBindPoint.Graphics,
      pipelineLayout,
      firstSet,
      setCount,
      &descriptorSet,
      0,
      null
    );
  }

  public static unsafe void BindDescriptorSet(
    VulkanDevice device,
    VkDescriptorSet descriptorSet,
    nint commandBuffer,
    VkPipelineLayout pipelineLayout,
    uint firstSet,
    uint setCount
  ) {
    device.DeviceApi.vkCmdBindDescriptorSets(
      commandBuffer,
      VkPipelineBindPoint.Graphics,
      pipelineLayout,
      firstSet,
      setCount,
      &descriptorSet,
      0,
      null
    );
  }

  public static unsafe void BindDescriptorSets(
    VulkanDevice device,
    VkDescriptorSet[] descriptorSets,
    FrameInfo frameInfo,
    VkPipelineLayout pipelineLayout,
    uint firstSet
  ) {
    device.DeviceApi.vkCmdBindDescriptorSets(
      frameInfo.CommandBuffer,
      VkPipelineBindPoint.Graphics,
      pipelineLayout,
      firstSet,
      descriptorSets
    );
  }
}