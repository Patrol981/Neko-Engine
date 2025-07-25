using System.Collections.Concurrent;
using Dwarf.AbstractionLayer;
using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Rendering;

public class BufferPool : IDisposable {
  private readonly nint _allocator;
  private readonly IDevice _device;

  private readonly ConcurrentDictionary<uint, DwarfBuffer> _buffers = [];
  private const ulong MAX_SIZE = VK_WHOLE_SIZE;

  public BufferPool(Application app) {
    _device = app.Device;
    _allocator = app.Allocator;
  }

  public BufferPool(IDevice device, nint allocator) {
    _device = device;
    _allocator = allocator;
  }

  public unsafe bool AddToBuffer(uint index, in DwarfBuffer inBuffer, out ulong offest) {
    offest = 0;
    if (!_buffers.ContainsKey(index)) return false;

    _buffers.TryGetValue(index, out var buffer);

    if (buffer == null) {
      return CreateNewBuffer(index, inBuffer);
    } else {
      offest = buffer.GetBufferSize();
      return AddToExistingBuffer(ref buffer, inBuffer);
    }
  }

  private bool CreateNewBuffer(in uint index, in DwarfBuffer inBuffer) {
    var stagingBuffer = new DwarfBuffer(
      _allocator,
      _device,
      inBuffer.GetBufferSize(),
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
      stagingBuffer: true,
      cpuAccessible: true
    );

    stagingBuffer.Map(inBuffer.GetBufferSize());
    stagingBuffer.WrtieToIndex((nint)inBuffer.GetBuffer().Handle, 0);

    var newBuffer = new DwarfBuffer(
      _allocator,
      _device,
      inBuffer.GetBufferSize(),
      BufferUsage.TransferDst | BufferUsage.VertexBuffer,
      MemoryProperty.DeviceLocal
    );
    _device.CopyBuffer(stagingBuffer.GetBuffer(), inBuffer.GetBuffer(), inBuffer.GetBufferSize());
    stagingBuffer.Dispose();
    _buffers[index] = newBuffer;

    return true;
  }

  private bool AddToExistingBuffer(ref DwarfBuffer buffer, in DwarfBuffer inBuffer) {
    var sumSize = buffer.GetBufferSize() + inBuffer.GetBufferSize();
    if (sumSize > MAX_SIZE) return false;

    var stagingBuffer = new DwarfBuffer(
      _allocator,
      _device,
      sumSize,
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
      stagingBuffer: true,
      cpuAccessible: true
    );

    stagingBuffer.Map(sumSize);
    stagingBuffer.WrtieToIndex((nint)buffer.GetBuffer().Handle, 0);
    stagingBuffer.WrtieToIndex((nint)inBuffer.GetBuffer().Handle, buffer.GetBufferSize());

    buffer.Dispose();
    buffer = new DwarfBuffer(
      _allocator,
      _device,
      sumSize,
      BufferUsage.TransferDst | BufferUsage.VertexBuffer,
      MemoryProperty.DeviceLocal
    );

    _device.CopyBuffer(stagingBuffer.GetBuffer(), buffer.GetBuffer(), sumSize);
    stagingBuffer.Dispose();
    return true;
  }

  public int AddToPool() {
    var key = (uint)_buffers.Keys.Count;
    var addResult = _buffers.TryAdd(key, null!);
    if (addResult) {
      return (int)key;
    } else {
      return -1;
    }
  }

  public void Dispose() {
    foreach (var buff in _buffers.Values) {
      buff.Dispose();
    }
    _buffers.Clear();
    GC.SuppressFinalize(this);
  }
}