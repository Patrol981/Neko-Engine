using Neko.Utils;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vma;
using static Vortice.Vulkan.Vulkan;

namespace Neko.AbstractionLayer;

public enum AllocationStrategy {
  VulkanMemoryAllocator,
  Vulkan,
  Metal,
}

public unsafe class DwarfBuffer : IDisposable {
  private readonly IDevice _device;
  private nint _allocator;
  private nint _allocation;

  private void* _mapped;
  private ulong _buffer = 0;
  private ulong _memory = 0;

  private readonly ulong _bufferSize;
  private readonly ulong _instanceCount;
  private readonly ulong _instanceSize;
  private readonly ulong _alignmentSize;
  private readonly ulong _offsetAlignment;
  private readonly BufferUsage _usageFlags;
  private readonly MemoryProperty _memoryPropertyFlags;

  private readonly AllocationStrategy _allocationStrategy;

  private readonly bool _isStagingBuffer = false;

  public DwarfBuffer(
    nint allocator,
    IDevice device,
    ulong instanceSize,
    ulong instanceCount,
    BufferUsage usageFlags,
    MemoryProperty propertyFlags,
    ulong minOffsetAlignment = 1,
    bool stagingBuffer = false,
    bool cpuAccessible = true,
    AllocationStrategy allocationStrategy = AllocationStrategy.VulkanMemoryAllocator,
    VkDeviceApi deviceApi = null!
  ) {
    _device = device;
    _instanceSize = instanceSize;
    _instanceCount = instanceCount;
    _usageFlags = usageFlags;
    _memoryPropertyFlags = propertyFlags;
    _offsetAlignment = minOffsetAlignment;

    _alignmentSize = GetAlignment(instanceSize, minOffsetAlignment);
    _bufferSize = _alignmentSize * _instanceCount;
    _isStagingBuffer = stagingBuffer;
    _allocationStrategy = allocationStrategy;

    if (allocationStrategy == AllocationStrategy.Vulkan) {
      _device.CreateBuffer(_bufferSize, _usageFlags, _memoryPropertyFlags, out _buffer, out _memory);
      _deviceApi = deviceApi;
      return;
    }

    if (allocator == IntPtr.Zero) throw new ArgumentNullException(nameof(allocator));

    // VmaAllocationCreateInfo allocationCreateInfo = new() {
    //   usage = VmaMemoryUsage.Auto
    // };
    // if (cpuAccessible) {
    //   allocationCreateInfo.flags = VmaAllocationCreateFlags.HostAccessSequentialWrite |
    //                                VmaAllocationCreateFlags.Mapped;
    // }

    // VkBufferCreateInfo bufferInfo = new() {
    //   size = _bufferSize,
    //   usage = (VkBufferUsageFlags)usageFlags,
    //   sharingMode = VkSharingMode.Exclusive
    // };

    // if (vmaCreateBuffer(allocator, in bufferInfo, in allocationCreateInfo, out _buffer, out _allocation) != VkResult.Success) {
    //   throw new Exception("Failed to create buffer!");
    // }
    CreateVmaBuffer(allocator, _bufferSize, usageFlags, cpuAccessible);
    _allocator = allocator;
  }

  public NekoBuffer(
    VmaAllocator allocator,
    IDevice device,
    ulong bufferSize,
    BufferUsage usageFlags,
    MemoryProperty propertyFlags,
    bool stagingBuffer = false,
    bool cpuAccessible = true,
    AllocationStrategy allocationStrategy = AllocationStrategy.VulkanMemoryAllocator,
    VkDeviceApi deviceApi = null!
  ) {
    _device = device;
    _instanceSize = bufferSize;
    _instanceCount = 1;
    _usageFlags = usageFlags;
    _memoryPropertyFlags = propertyFlags;
    _alignmentSize = bufferSize;
    _offsetAlignment = 1;
    _bufferSize = bufferSize;
    _isStagingBuffer = stagingBuffer;
    _allocationStrategy = allocationStrategy;

    if (allocationStrategy == AllocationStrategy.Vulkan) {
      _device.CreateBuffer(_bufferSize, _usageFlags, _memoryPropertyFlags, out _buffer, out _memory);
      _deviceApi = deviceApi;
      return;
    }

    if (allocator.IsNull) throw new ArgumentNullException(nameof(allocator));

    // VmaAllocationCreateInfo allocationCreateInfo = new() {
    //   usage = VmaMemoryUsage.Auto
    // };
    // if (cpuAccessible) {
    //   allocationCreateInfo.flags = VmaAllocationCreateFlags.HostAccessSequentialWrite |
    //                                VmaAllocationCreateFlags.Mapped;
    // }

    // VkBufferCreateInfo bufferInfo = new() {
    //   size = _bufferSize,
    //   usage = (VkBufferUsageFlags)usageFlags,
    //   sharingMode = VkSharingMode.Exclusive
    // };
    // if (vmaCreateBuffer(allocator, in bufferInfo, in allocationCreateInfo, out _buffer, out _allocation) != VkResult.Success) {
    //   throw new Exception("Failed to create buffer!");
    // }
    CreateVmaBuffer(allocator, _bufferSize, usageFlags, cpuAccessible);
    _allocator = allocator;
  }

  private void CreateVmaBuffer(nint allocator, ulong size, BufferUsage bufferUsageFlags, bool cpuAccessible) {
    VkBufferCreateInfo bufferInfo = new() {
      size = _bufferSize,
      usage = (VkBufferUsageFlags)bufferUsageFlags,
      sharingMode = VkSharingMode.Exclusive
    };

    VmaAllocationCreateInfo allocationCreateInfo = new() {
      usage = VmaMemoryUsage.Auto
    };
    if (cpuAccessible) {
      allocationCreateInfo.flags = VmaAllocationCreateFlags.HostAccessSequentialWrite |
                                   VmaAllocationCreateFlags.Mapped;
    }

    VkBuffer buffer;
    VmaAllocation vmaAllocation;
    vmaCreateBuffer(allocator, &bufferInfo, &allocationCreateInfo, &buffer, &vmaAllocation, default).CheckResult();
    _buffer = buffer;
    _allocation = vmaAllocation;

    // if (vmaCreateBuffer(allocator, in bufferInfo, in allocationCreateInfo, out _buffer, out _allocation) != VkResult.Success) {
    //   throw new Exception("Failed to create buffer!");
    // }
  }

  public void Map(ulong size = VK_WHOLE_SIZE, ulong offset = 0) {
    fixed (void** ptr = &_mapped) {
      switch (_allocationStrategy) {
        case AllocationStrategy.VulkanMemoryAllocator:
          vmaMapMemory(_allocator, _allocation, ptr);
          break;
        case AllocationStrategy.Vulkan:
          vkMapMemory(_device.LogicalDevice, _memory, offset, size, VkMemoryMapFlags.None, ptr).CheckResult();
          break;
      }
    }
  }

  /*
  public static void Map(NekoBuffer buff, ulong size = VK_WHOLE_SIZE, ulong offset = 0) {
    fixed (void** ptr = &buff._mapped) {
      vkMapMemory(buff._device.LogicalDevice, buff._memory, offset, size, VkMemoryMapFlags.None, ptr).CheckResult();
    }
  }
  */

  public void Unmap() {
    if (_mapped != null) {
      switch (_allocationStrategy) {
        case AllocationStrategy.VulkanMemoryAllocator:
          vmaUnmapMemory(_allocator, _allocation);
          break;
        case AllocationStrategy.Vulkan:
          vkUnmapMemory(_device.LogicalDevice, _memory);
          break;
      }
      _mapped = null;
    }
  }

  public void WriteToBuffer(nint data, ulong size = VK_WHOLE_SIZE, ulong offset = 0) {
    if (size == VK_WHOLE_SIZE) {
      MemoryUtils.MemCopy(_mapped, (void*)data, (int)_bufferSize);
    } else {
      if (size <= 0) {
        return;
      }
      char* memOffset = (char*)_mapped;
      memOffset += offset;
      MemoryUtils.MemCopy((nint)memOffset, data, (int)size);
    }
  }

  public VkResult Flush(ulong size = VK_WHOLE_SIZE, ulong offset = 0) {
    VkMappedMemoryRange mappedRange = new() {
      memory = _memory,
      offset = offset,
      size = size
    };
    return _allocationStrategy switch {
      AllocationStrategy.VulkanMemoryAllocator => vmaFlushAllocation(_allocator, _allocation, offset, size),
      AllocationStrategy.Vulkan => vkFlushMappedMemoryRanges(_device.LogicalDevice, 1, &mappedRange),
      _ => throw new NotImplementedException(),
    };
  }

  public VkDescriptorBufferInfo GetDescriptorBufferInfo(ulong size = VK_WHOLE_SIZE, ulong offset = 0) {
    VkDescriptorBufferInfo bufferInfo = new() {
      buffer = _buffer,
      offset = offset,
      range = size
    };
    return bufferInfo;
  }

  public VkResult Invalidate(ulong size = VK_WHOLE_SIZE, ulong offset = 0) {
    VkMappedMemoryRange mappedRange = new() {
      memory = _memory,
      offset = offset,
      size = size
    };
    return _allocationStrategy switch {
      AllocationStrategy.VulkanMemoryAllocator => vmaInvalidateAllocation(_allocator, _allocation, offset, size),
      AllocationStrategy.Vulkan => vkInvalidateMappedMemoryRanges(_device.LogicalDevice, 1, &mappedRange),
      _ => throw new NotImplementedException(),
    };
  }

  public void WrtieToIndex(nint data, int index) {
    WriteToBuffer(data, _instanceSize, (ulong)index * _alignmentSize);
  }

  public void WrtieToIndex(nint data, ulong index) {
    WriteToBuffer(data, _instanceSize, index * _alignmentSize);
  }

  public VkResult FlushIndex(int index) {
    return Flush(_alignmentSize, (ulong)index * _alignmentSize);
  }

  public VkDescriptorBufferInfo GetVkDescriptorBufferInfoForIndex(int index) {
    return GetDescriptorBufferInfo(_alignmentSize, (ulong)index * _alignmentSize);
  }

  public VkResult InvalidateIndex(int index) {
    return Invalidate(_alignmentSize, (ulong)index * _alignmentSize);
  }

  public VkBuffer GetBuffer() {
    return _buffer;
  }

  public VkDeviceMemory GetVkDeviceMemory() {
    return _memory;
  }

  public void* GetMappedMemory() {
    return _mapped;
  }

  public ulong GetInstanceCount() {
    return _instanceCount;
  }

  public ulong GetInstanceSize() {
    return _instanceSize;
  }

  public ulong GetAlignmentSize() {
    return _alignmentSize;
  }

  public ulong GetOffsetAlignment() {
    return _offsetAlignment;
  }

  public BufferUsage GetVkBufferUsageFlags() {
    return _usageFlags;
  }

  public MemoryProperty GetVkMemoryPropertyFlags() {
    return _memoryPropertyFlags;
  }

  public ulong GetBufferSize() {
    return _bufferSize;
  }

  private static ulong GetAlignment(ulong instanceSize, ulong minOffsetAlignment) {
    return minOffsetAlignment > 0 ? (instanceSize + minOffsetAlignment - 1) & ~(minOffsetAlignment - 1) : instanceSize;
  }

  public void FreeMemory() {
    if (_memory <= 0) return;
    switch (_allocationStrategy) {
      case AllocationStrategy.VulkanMemoryAllocator:
        vmaFreeMemory(_allocator, _allocation);
        break;
      case AllocationStrategy.Vulkan:
        vkFreeMemory(_device.LogicalDevice, _memory);
        break;
    }
  }

  public void DestoryBuffer() {
    if (_buffer <= 0) return;
    switch (_allocationStrategy) {
      case AllocationStrategy.VulkanMemoryAllocator:
        vmaDestroyBuffer(_allocator, _buffer, _allocation);
        break;
      case AllocationStrategy.Vulkan:
        vkDestroyBuffer(_device.LogicalDevice, _buffer);
        break;
    }
  }
  public void Dispose() {
    if (!_isStagingBuffer) {
      _device.WaitAllQueues();
    }
    Unmap();
    DestoryBuffer();
    FreeMemory();
    GC.SuppressFinalize(this);
  }
}