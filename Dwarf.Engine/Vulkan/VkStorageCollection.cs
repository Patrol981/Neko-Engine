using System.Runtime.CompilerServices;

using Dwarf.AbstractionLayer;
using Dwarf.Extensions.Logging;
using Dwarf.Rendering.Renderer3D;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Vulkan;

public struct StorageData {
  public VkDescriptorSet[] Descriptors;
  public DwarfBuffer[] Buffers;
  public VkDescriptorType DescriptorType;
  public BufferUsage BufferUsage;
}

public class VkStorageCollection : IStorageCollection {
  private readonly VulkanDevice _device = null!;
  private readonly nint _allocator = IntPtr.Zero;
  private readonly VulkanDescriptorPool _dynamicPool = null!;

  public VkStorageCollection(nint allocator, VulkanDevice device) {
    _device = device;
    _allocator = allocator;

    _dynamicPool = new VulkanDescriptorPool.Builder(_device)
      .SetMaxSets(30)
      .AddPoolSize(DescriptorType.StorageBuffer, 30)
      .SetPoolFlags(DescriptorPoolCreateFlags.UpdateAfterBind)
      .Build();
  }

  public unsafe void CreateStorage(
    IDevice device,
    DescriptorType descriptorType,
    BufferUsage usageType,
    int arraySize,
    ulong bufferSize,
    ulong bufferCount,
    IDescriptorSetLayout layout,
    IDescriptorPool pool,
    string storageName,
    ulong offsetAlignment,
    bool mapWholeBuffer = false
  ) {
    if (bufferCount == 0) bufferCount = 1;

    pool ??= _dynamicPool;

    var storage = new StorageData {
      Buffers = new DwarfBuffer[arraySize]
    };
    for (int i = 0; i < arraySize; i++) {
      storage.Buffers[i] = new(
        _allocator,
        device,
        bufferSize,
        bufferCount,
        usageType,
        MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
        device.MinStorageBufferOffsetAlignment
      );
      if (mapWholeBuffer) {
        storage.Buffers[i].Map();
      } else {
        storage.Buffers[i].Map(bufferSize * bufferCount);
      }
    }

    storage.Descriptors = new VkDescriptorSet[arraySize];
    for (int i = 0; i < storage.Descriptors.Length; i++) {
      var bufferInfo = mapWholeBuffer ?
        storage.Buffers[i].GetDescriptorBufferInfo() :
        storage.Buffers[i].GetDescriptorBufferInfo(bufferSize * bufferCount);
      _ = new VulkanDescriptorWriter((VulkanDescriptorSetLayout)layout, (VulkanDescriptorPool)pool)
        .WriteBuffer(0, &bufferInfo)
        .Build(out storage.Descriptors[i]);
    }

    Storages.TryAdd(storageName, storage);
  }

  public unsafe void CheckSize(
    string key,
    int index,
    int elemCount,
    IDescriptorSetLayout layout,
    bool mapWholeBuffer = false) {
    if (!Storages.TryGetValue(key, out var storageData)) return;
    if (storageData.Buffers.Length < index) return;
    if (elemCount < 1) return;
    var buff = storageData.Buffers[index];

    if (buff.GetBufferSize() < buff.GetAlignmentSize() * (ulong)elemCount ||
      buff.GetInstanceCount() > (ulong)elemCount
    ) {
      Storages[key].Buffers[index]?.Dispose();
      Storages[key].Buffers[index] = new(
        _allocator,
        _device,
        (ulong)Unsafe.SizeOf<ObjectData>(),
        (ulong)elemCount,
        BufferUsage.StorageBuffer,
        MemoryProperty.HostVisible | MemoryProperty.HostCoherent
      );
      Storages[key].Buffers[index].Map();

      _dynamicPool.FreeDescriptors([Storages[key].Descriptors[index]]);

      var bufferInfo = Storages[key].Buffers[index].GetDescriptorBufferInfo();
      _ = new VulkanDescriptorWriter((VulkanDescriptorSetLayout)layout, _dynamicPool)
        .WriteBuffer(0, &bufferInfo)
        .Build(out Storages[key].Descriptors[index]);

      Logger.Info($"[Storage Collection] Updated Sizes of {key}[{index}].");
    }
  }

  public void WriteBuffer(string key, int index, nint data, ulong size = VK_WHOLE_SIZE) {
    if (!Storages.TryGetValue(key, out var storage)) return;
    if (storage.Buffers[index] == null) return;
    Application.Mutex.WaitOne();
    Storages[key].Buffers[index].WriteToBuffer(data, size);
    Application.Mutex.ReleaseMutex();
  }

  public ulong GetDescriptor(string key, int index) {
    // Storages[key].Descriptors[index]
    return Storages.TryGetValue(key, out var storageData)
      ? storageData.Descriptors[index] != VkDescriptorSet.Null ? storageData.Descriptors[index] : VkDescriptorSet.Null
      : VkDescriptorSet.Null;
  }

  public void Dispose() {
    _dynamicPool?.Dispose();
    foreach (var storage in Storages.Values) {
      foreach (var buffer in storage.Buffers) {
        buffer.Dispose();
      }
    }
  }

  public Dictionary<string, StorageData> Storages { get; private set; } = [];
}

