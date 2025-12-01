using System.Numerics;
using System.Runtime.CompilerServices;
using Neko.AbstractionLayer;
using Neko.Rendering;
using Neko.Rendering.Lightning;
using Neko.Rendering.Renderer2D.Models;
using Neko.Rendering.Renderer3D;
using Neko.Vulkan;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vma;

namespace Neko;

public class ResourceInitializer {

  public static void InitAllocator(IDevice device, out nint allocator) {
    allocator = device.RenderAPI switch {
      RenderAPI.Vulkan => VkInitAllocator(device),
      RenderAPI.Metal => IntPtr.Zero,
      _ => throw new NotImplementedException(),
    };
  }

  private static nint VkInitAllocator(IDevice device) {
    var vkDevice = (VulkanDevice)device;
    VmaAllocatorCreateFlags allocatorFlags = VmaAllocatorCreateFlags.KHRDedicatedAllocation | VmaAllocatorCreateFlags.KHRBindMemory2;
    VmaAllocatorCreateInfo allocatorCreateInfo = new() {
      flags = allocatorFlags,
      instance = vkDevice.VkInstance,
      vulkanApiVersion = VkVersion.Version_1_4,
      physicalDevice = vkDevice.PhysicalDevice,
      device = vkDevice.LogicalDevice,
    };
    vmaCreateAllocator(allocatorCreateInfo, out var allocator);
    return allocator;
  }

  public static void DestroyAllocator(IDevice device, nint allocator) {
    switch (device.RenderAPI) {
      case RenderAPI.Vulkan:
        VkDestroyAllocator(allocator);
        break;
      case RenderAPI.Metal:
        break;
      default:
        throw new NotImplementedException();
    }
  }

  private static void VkDestroyAllocator(VmaAllocator vmaAllocator) {
    vmaDestroyAllocator(vmaAllocator);
  }

  public static void InitResources(
    in IDevice device,
    in IRenderer renderer,
    in IStorageCollection storageCollection,
    ref IDescriptorPool globalPool,
    ref Dictionary<string, IDescriptorSetLayout> descriptorSetLayouts
  ) {
    switch (device.RenderAPI) {
      case RenderAPI.Vulkan:
        VkInitResources(device, renderer, storageCollection, ref globalPool, ref descriptorSetLayouts);
        break;
      case RenderAPI.Metal:
        throw new NotImplementedException();
      default:
        throw new NotImplementedException();
    }
  }

  public static void SetupResources(
    in IDevice device,
    in IRenderer renderer,
    in SystemCollection systems,
    in IStorageCollection storageCollection,
    ref IDescriptorPool globalPool,
    ref Dictionary<string, IDescriptorSetLayout> descriptorSetLayouts,
    bool useSkybox
  ) {
    switch (device.RenderAPI) {
      case RenderAPI.Vulkan:
        VkSetupResources(
          device,
          renderer,
          systems,
          storageCollection,
          ref globalPool,
          ref descriptorSetLayouts,
          useSkybox
        );
        break;
      case RenderAPI.Metal:
        throw new NotImplementedException();
      default:
        throw new NotImplementedException();
    }
  }

  private static void VkInitResources(
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
      .AddPoolSize(DescriptorType.StorageBuffer, (uint)renderer.MAX_FRAMES_IN_FLIGHT * 100)
      .SetPoolFlags(DescriptorPoolCreateFlags.UpdateAfterBind)
      .Build();

    descriptorSetLayouts.TryAdd("Global", new VulkanDescriptorSetLayout.Builder((VulkanDevice)device)
     .AddBinding(0, DescriptorType.UniformBuffer, ShaderStageFlags.AllGraphics)
     .Build());

    descriptorSetLayouts.TryAdd("PointLight", new VulkanDescriptorSetLayout.Builder((VulkanDevice)device)
      .AddBinding(0, DescriptorType.StorageBuffer, ShaderStageFlags.AllGraphics)
      .Build());

    descriptorSetLayouts.TryAdd("ObjectData", new VulkanDescriptorSetLayout.Builder((VulkanDevice)device)
      .AddBinding(0, DescriptorType.StorageBuffer, ShaderStageFlags.AllGraphics)
      // .AddBinding(1, VkDescriptorType.StorageBuffer, VkShaderStageFlags.AllGraphics)
      .Build());

    descriptorSetLayouts.TryAdd("CustomShaderObjectData", new VulkanDescriptorSetLayout.Builder((VulkanDevice)device)
      .AddBinding(0, DescriptorType.StorageBuffer, ShaderStageFlags.AllGraphics)
      .Build());

    descriptorSetLayouts.TryAdd("SpriteData", new VulkanDescriptorSetLayout.Builder((VulkanDevice)device)
      .AddBinding(0, DescriptorType.StorageBuffer, ShaderStageFlags.AllGraphics)
      .Build());

    descriptorSetLayouts.TryAdd("TileLayerData", new VulkanDescriptorSetLayout.Builder((VulkanDevice)device)
    .AddBinding(0, DescriptorType.StorageBuffer, ShaderStageFlags.AllGraphics)
    .Build());

    descriptorSetLayouts.TryAdd("CustomSpriteData", new VulkanDescriptorSetLayout.Builder((VulkanDevice)device)
      .AddBinding(0, DescriptorType.StorageBuffer, ShaderStageFlags.AllGraphics)
      .Build());

    descriptorSetLayouts.TryAdd("JointsBuffer", new VulkanDescriptorSetLayout.Builder((VulkanDevice)device)
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
      CommonConstants.MAX_POINT_LIGHTS_COUNT,
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

  private static void VkSetupResources(
    in IDevice device,
    in IRenderer renderer,
    in SystemCollection systems,
    in IStorageCollection storageCollection,
    ref IDescriptorPool globalPool,
    ref Dictionary<string, IDescriptorSetLayout> descriptorSetLayouts,
    bool useSkybox
  ) {
    // if (systems.Render3DSystem != null) {
    if (systems.StaticRenderSystem != null && systems.SkinnedRenderSystem != null) {
      storageCollection.CreateStorage(
        device,
        DescriptorType.StorageBuffer,
        BufferUsage.StorageBuffer,
        renderer.MAX_FRAMES_IN_FLIGHT,
        (ulong)Unsafe.SizeOf<ObjectData>(),
        (ulong)systems.StaticRenderSystem.LastKnownElemCount,
        (VulkanDescriptorSetLayout)descriptorSetLayouts["ObjectData"],
        null!,
        "StaticObjectStorage",
        device.MinStorageBufferOffsetAlignment,
        true
      );

      storageCollection.CreateStorage(
        device,
        DescriptorType.StorageBuffer,
        BufferUsage.StorageBuffer,
        renderer.MAX_FRAMES_IN_FLIGHT,
        (ulong)Unsafe.SizeOf<ObjectData>(),
        (ulong)systems.StaticRenderSystem.LastKnownElemCount,
        (VulkanDescriptorSetLayout)descriptorSetLayouts["ObjectData"],
        null!,
        "SkinnedObjectStorage",
        device.MinStorageBufferOffsetAlignment,
        true
      );

      storageCollection.CreateStorage(
        device,
        DescriptorType.StorageBuffer,
        BufferUsage.StorageBuffer,
        renderer.MAX_FRAMES_IN_FLIGHT,
        (ulong)Unsafe.SizeOf<Matrix4x4>(),
        systems.SkinnedRenderSystem.LastKnownSkinnedElemJointsCount,
        (VulkanDescriptorSetLayout)descriptorSetLayouts["JointsBuffer"],
        null!,
        "JointsStorage",
        device.MinStorageBufferOffsetAlignment,
        true
      );

      storageCollection.CreateStorage(
        device,
        DescriptorType.StorageBuffer,
        BufferUsage.StorageBuffer,
        renderer.MAX_FRAMES_IN_FLIGHT,
        (ulong)Unsafe.SizeOf<ObjectData>(),
        (ulong)(systems.CustomShaderRender3DSystem?.LastKnownElemCount ?? 0),
        (VulkanDescriptorSetLayout)descriptorSetLayouts["CustomShaderObjectData"],
        null!,
        "CustomShaderObjectStorage",
        device.MinStorageBufferOffsetAlignment,
        true
      );
    }

    if (systems.Render2DSystem != null) {
      storageCollection.CreateStorage(
        device,
        DescriptorType.StorageBuffer,
        BufferUsage.StorageBuffer,
        renderer.MAX_FRAMES_IN_FLIGHT,
        (ulong)Unsafe.SizeOf<SpritePushConstant140>(),
        (ulong)systems.Render2DSystem.LastKnownElemCount,
        (VulkanDescriptorSetLayout)descriptorSetLayouts["SpriteData"],
        null!,
        "SpriteStorage",
        device.MinStorageBufferOffsetAlignment,
        true
      );

      storageCollection.CreateStorage(
        device,
        DescriptorType.StorageBuffer,
        BufferUsage.StorageBuffer,
        renderer.MAX_FRAMES_IN_FLIGHT,
        (ulong)Unsafe.SizeOf<SpritePushConstant140>(),
        (ulong)(systems.CustomShaderRender2DSystem?.LastKnownElemCount ?? 0),
        (VulkanDescriptorSetLayout)descriptorSetLayouts["CustomSpriteData"],
        null!,
        "CustomSpriteStorage",
        device.MinStorageBufferOffsetAlignment,
        true
      );
    }

    if (systems.TilemapRenderSystem != null) {
      storageCollection.CreateStorage(
        device,
        DescriptorType.StorageBuffer,
        BufferUsage.StorageBuffer,
        renderer.MAX_FRAMES_IN_FLIGHT,
        (ulong)Unsafe.SizeOf<SpritePushConstant140>(),
        (ulong)systems.TilemapRenderSystem.LastKnownLayerCount,
        (VulkanDescriptorSetLayout)descriptorSetLayouts["TileLayerData"],
        null!,
        "TileLayerStorage",
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