using System.Numerics;
using System.Runtime.CompilerServices;
using Dwarf.AbstractionLayer;
using Dwarf.Rendering;
using Dwarf.Rendering.Lightning;
using Dwarf.Rendering.Renderer3D;
using Dwarf.Vulkan;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vma;

namespace Dwarf;

public class ResourceInitializer {
  public static void VkInitAllocator(IDevice device, out VmaAllocator allocator) {
    var vkDevice = (VulkanDevice)device;
    VmaAllocatorCreateFlags allocatorFlags = VmaAllocatorCreateFlags.KHRDedicatedAllocation | VmaAllocatorCreateFlags.KHRBindMemory2;
    VmaAllocatorCreateInfo allocatorCreateInfo = new() {
      flags = allocatorFlags,
      instance = vkDevice.VkInstance,
      vulkanApiVersion = VkVersion.Version_1_4,
      physicalDevice = vkDevice.PhysicalDevice,
      device = vkDevice.LogicalDevice,
    };
    vmaCreateAllocator(allocatorCreateInfo, out allocator);
  }

  public static void VkInitResources(
    in IDevice device,
    in IRenderer renderer,
    in IStorageCollection storageCollection,
    ref IDescriptorPool globalPool,
    ref Dictionary<string, IDescriptorSetLayout> descriptorSetLayouts
  ) {
    globalPool = new VulkanDescriptorPool.Builder((VulkanDevice)device)
      .SetMaxSets(10)
      .AddPoolSize(DescriptorType.UniformBuffer, (uint)renderer.MAX_FRAMES_IN_FLIGHT)
      .AddPoolSize(DescriptorType.CombinedImageSampler, (uint)renderer.MAX_FRAMES_IN_FLIGHT)
      .AddPoolSize(DescriptorType.InputAttachment, (uint)renderer.MAX_FRAMES_IN_FLIGHT)
      .AddPoolSize(DescriptorType.SampledImage, (uint)renderer.MAX_FRAMES_IN_FLIGHT)
      .AddPoolSize(DescriptorType.Sampler, (uint)renderer.MAX_FRAMES_IN_FLIGHT)
      .AddPoolSize(DescriptorType.StorageBuffer, (uint)renderer.MAX_FRAMES_IN_FLIGHT * 45)
      .Build();

    descriptorSetLayouts.TryAdd("Global", new VulkanDescriptorSetLayout.Builder(device)
     .AddBinding(0, DescriptorType.UniformBuffer, ShaderStageFlags.AllGraphics)
     .Build());

    descriptorSetLayouts.TryAdd("PointLight", new VulkanDescriptorSetLayout.Builder(device)
      .AddBinding(0, DescriptorType.StorageBuffer, ShaderStageFlags.AllGraphics)
      .Build());

    descriptorSetLayouts.TryAdd("ObjectData", new VulkanDescriptorSetLayout.Builder(device)
      .AddBinding(0, DescriptorType.StorageBuffer, ShaderStageFlags.Vertex)
      // .AddBinding(1, VkDescriptorType.StorageBuffer, VkShaderStageFlags.AllGraphics)
      .Build());

    descriptorSetLayouts.TryAdd("JointsBuffer", new VulkanDescriptorSetLayout.Builder(device)
      .AddBinding(0, DescriptorType.StorageBuffer, ShaderStageFlags.Vertex)
      .Build());

    // _descriptorSetLayouts.TryAdd("InputAttachments", new DescriptorSetLayout.Builder(Device)
    //   .AddBinding(0, VkDescriptorType.InputAttachment, VkShaderStageFlags.Fragment)
    //   .Build());

    // StorageCollection.CreateStorage(
    //   Device,
    //   VkDescriptorType.InputAttachment,
    //   BufferUsage.UniformBuffer
    // )

    storageCollection.CreateStorage(
      device,
      DescriptorType.UniformBuffer,
      BufferUsage.UniformBuffer,
      renderer.MAX_FRAMES_IN_FLIGHT,
      (ulong)Unsafe.SizeOf<GlobalUniformBufferObject>(),
      1,
      descriptorSetLayouts["Global"],
      globalPool,
      "GlobalStorage",
      device.MinUniformBufferOffsetAlignment,
      default
    );

    storageCollection.CreateStorage(
      device,
      DescriptorType.StorageBuffer,
      BufferUsage.StorageBuffer,
      renderer.MAX_FRAMES_IN_FLIGHT,
      (ulong)Unsafe.SizeOf<PointLight>(),
      Application.MAX_POINT_LIGHTS_COUNT,
      descriptorSetLayouts["PointLight"],
      globalPool,
      "PointStorage",
      device.MinStorageBufferOffsetAlignment,
      default
    );

    //_descriptorSetLayouts.TryAdd("Texture", new DescriptorSetLayout.Builder(Device)
    //  .AddBinding(0, VkDescriptorType.CombinedImageSampler, VkShaderStageFlags.Fragment)
    //  .Build());
  }

  public static void VkSetupResources(
    in IDevice device,
    in IRenderer renderer,
    in SystemCollection systems,
    in IStorageCollection storageCollection,
    ref IDescriptorPool globalPool,
    ref Dictionary<string, IDescriptorSetLayout> descriptorSetLayouts,
    bool useSkybox
  ) {
    if (systems.Render3DSystem != null) {
      storageCollection.CreateStorage(
        device,
        DescriptorType.StorageBuffer,
        BufferUsage.StorageBuffer,
        renderer.MAX_FRAMES_IN_FLIGHT,
        (ulong)Unsafe.SizeOf<ObjectData>(),
        (ulong)systems.Render3DSystem.LastKnownElemCount,
        (VulkanDescriptorSetLayout)descriptorSetLayouts["ObjectData"],
        null!,
        "ObjectStorage",
        device.MinStorageBufferOffsetAlignment,
        true
      );

      storageCollection.CreateStorage(
        device,
        DescriptorType.StorageBuffer,
        BufferUsage.StorageBuffer,
        renderer.MAX_FRAMES_IN_FLIGHT,
        (ulong)Unsafe.SizeOf<Matrix4x4>(),
        systems.Render3DSystem.LastKnownSkinnedElemJointsCount,
        (VulkanDescriptorSetLayout)descriptorSetLayouts["JointsBuffer"],
        null!,
        "JointsStorage",
        device.MinStorageBufferOffsetAlignment,
        true
      );
    }

    // if (useSkybox) {
    //   _skybox = new(
    //     _allocator,
    //     Device,
    //     _textureManager,
    //     Renderer,
    //     _descriptorSetLayouts["Global"].GetDescriptorSetLayout()
    //   );
    // }
  }
}