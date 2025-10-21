using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Dwarf.AbstractionLayer;
using Dwarf.Extensions.Logging;
using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Rendering;

public class BufferPool : IDisposable {
  private readonly nint _allocator;
  private readonly IDevice _device;

  internal class BufferData {
    internal DwarfBuffer VertexBuffer = null!;
    internal DwarfBuffer IndexBuffer = null!;
  };

  private readonly ConcurrentDictionary<uint, BufferData> _buffers = [];
  // private const ulong MAX_BUFF_SIZE = 2500000;
  // private readonly ulong MAX_HEAP_SIZE = 268435456;
  // 2500000
  private ulong _maxBufferSize = 0;

  public BufferPool(Application app) {
    _device = app.Device;
    _allocator = app.Allocator;
    _maxBufferSize = _device.MaxBufferSize / 2;
  }

  public BufferPool(IDevice device, nint allocator) {
    _device = device;
    _allocator = allocator;
    _maxBufferSize = _device.MaxBufferSize / 2;
  }

  public bool CanAddToVertexBuffer(
    uint index,
    ulong byteSize,
    ulong prevSize
  ) {
    if (!_buffers.ContainsKey(index)) {
      return false;
    }

    _buffers.TryGetValue(index, out var buffer);

    if (buffer is null) throw new NullReferenceException("Buffer is null");
    if (buffer!.VertexBuffer == null) return false;

    var sumSize = prevSize + byteSize;
    if (sumSize >= _maxBufferSize) return false;
    if (prevSize > buffer.VertexBuffer.GetBufferSize()) return false;
    if (byteSize > buffer.VertexBuffer.GetBufferSize()) return false;

    return true;
  }

  public bool CanAddToIndexBuffer(
    uint index,
    ulong byteSize,
    ulong prevSize
  ) {
    if (!_buffers.ContainsKey(index)) return false;

    _buffers.TryGetValue(index, out var buffer);
    if (buffer!.IndexBuffer == null) return false;

    try {
      var sumSize = prevSize + byteSize;
      if (sumSize >= _maxBufferSize) return false;
      if (prevSize > buffer.IndexBuffer.GetBufferSize()) return false;
      if (byteSize > buffer.IndexBuffer.GetBufferSize()) return false;
    } catch (OverflowException) {
      return false;
    }

    return true;
  }

  public unsafe bool AddToBuffer(
    uint index,
    nint data,
    ulong byteSize,
    ulong prevSize,
    out ulong offset,
    out string reason
  ) {
    offset = 0;
    reason = "";
    if (!_buffers.ContainsKey(index)) {
      reason = "Key not found";
      return false;
    }

    _buffers.TryGetValue(index, out var buffer);

    if (buffer is null) throw new NullReferenceException("Buffer is null");

    if (buffer!.VertexBuffer == null) {
      return CreateNewVertexBuffer(index, data, byteSize);
    } else {
      offset = prevSize;
      // return AddToExistingBufferWithRebuild(index, ref buffer.VertexBuffer, data, byteSize, prevSize, ref reason);
      return AddToExistingBuffer(ref buffer.VertexBuffer, data, byteSize, prevSize, ref reason);
    }
  }

  public unsafe bool AddToIndex(
    uint index,
    nint data,
    ulong byteSize,
    ulong prevSize,
    out ulong offset,
    out string reason
  ) {
    offset = 0;
    reason = "";
    if (!_buffers.ContainsKey(index)) {
      reason = "Key not found";
      return false;
    }

    _buffers.TryGetValue(index, out var buffer);

    if (buffer!.IndexBuffer == null) {
      reason = "Created new buff";
      return CreateNewIndexBuffer(index, data, byteSize);
    } else {
      offset = prevSize;
      return AddToExistingIndex(in index, ref buffer.IndexBuffer, data, byteSize, prevSize, ref reason);
    }
  }

  public unsafe bool CreateNewIndexBuffer(in uint index, nint data, ulong byteSize) {
    var stagingBuffer = new DwarfBuffer(
      _allocator,
      _device,
      byteSize,
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
      stagingBuffer: true,
      cpuAccessible: true
    );

    stagingBuffer.Map();
    stagingBuffer.WriteToBuffer(data, byteSize);
    stagingBuffer.Unmap();

    var indexBuffer = new DwarfBuffer(
      _allocator,
      _device,
      _maxBufferSize,
      BufferUsage.IndexBuffer | BufferUsage.TransferDst,
      MemoryProperty.DeviceLocal
    );

    _device.CopyBuffer(
      stagingBuffer.GetBuffer(),
      indexBuffer.GetBuffer(),
      stagingBuffer.GetBufferSize()
    );
    stagingBuffer.Dispose();

    _buffers[index].IndexBuffer = indexBuffer;

    return true;
  }

  private bool CreateNewVertexBuffer(in uint index, nint data, ulong byteSize) {
    if (byteSize > _maxBufferSize) {
      throw new ArgumentOutOfRangeException(nameof(byteSize), $"input is larger than max size by {byteSize - _maxBufferSize} bytes");
    }

    var stagingBuffer = new DwarfBuffer(
      _allocator,
      _device,
      byteSize,
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
      stagingBuffer: true,
      cpuAccessible: true
    );

    stagingBuffer.Map();
    stagingBuffer.WriteToBuffer(data, byteSize);
    stagingBuffer.Unmap();

    var newBuffer = new DwarfBuffer(
      _allocator,
      _device,
      _maxBufferSize,
      BufferUsage.TransferDst | BufferUsage.VertexBuffer,
      MemoryProperty.DeviceLocal
    );
    _device.CopyBuffer(stagingBuffer.GetBuffer(), newBuffer.GetBuffer(), stagingBuffer.GetBufferSize());
    stagingBuffer.Dispose();
    _buffers[index].VertexBuffer = newBuffer;

    return true;
  }

  private unsafe bool AddToExistingIndex(in uint index, ref DwarfBuffer buffer, in nint data, in ulong byteSize, in ulong prevSize, ref string reason) {
    var sumSize = prevSize + byteSize;
    if (sumSize >= _maxBufferSize) {
      reason = $"sumSize of [{sumSize}] bytes is too large compared to maximum [{_maxBufferSize}] bytes";
      return false;
    }

    if (prevSize > buffer.GetBufferSize()) {
      reason = $"offset size is larger than the buffer itself [{prevSize}-{buffer.GetBufferSize()}={prevSize - buffer.GetBufferSize()}]";
      return false;
    }

    if (byteSize > buffer.GetBufferSize()) {
      reason = $"byte size is larger than the buffer itself [{byteSize}-{buffer.GetBufferSize()}={byteSize - buffer.GetBufferSize()}]";
      return false;
    }

    var stagingBuffer = new DwarfBuffer(
      _allocator,
      _device,
      sumSize,
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
      stagingBuffer: true,
      cpuAccessible: true
    );

    stagingBuffer.Map();
    stagingBuffer.WriteToBuffer(data, byteSize);
    stagingBuffer.Unmap();

    var newBuffer = new DwarfBuffer(
      _allocator,
      _device,
      sumSize,
      BufferUsage.TransferDst | BufferUsage.IndexBuffer,
      MemoryProperty.DeviceLocal
    );

    _device.CopyBuffer(
      srcBuffer: buffer.GetBuffer(),
      dstBuffer: newBuffer.GetBuffer(),
      size: prevSize,
      srcOffset: 0,
      dstOffset: 0
    );
    _device.CopyBuffer(
      srcBuffer: stagingBuffer.GetBuffer(),
      dstBuffer: newBuffer.GetBuffer(),
      size: byteSize,
      srcOffset: 0,
      dstOffset: prevSize
    );
    stagingBuffer.Dispose();
    buffer.Dispose();
    _buffers[index].IndexBuffer = newBuffer;

    return true;
  }

  private unsafe bool AddToExistingBufferWithRebuild(in uint index, ref DwarfBuffer buffer, in nint data, in ulong byteSize, in ulong prevSize, ref string reason) {
    var sumSize = prevSize + byteSize;
    if (sumSize >= _maxBufferSize) {
      reason = $"sumSize of [{sumSize}] bytes is too large compared to maximum [{_maxBufferSize}] bytes";
      return false;
    }

    if (prevSize > buffer.GetBufferSize()) {
      reason = $"offset size is larger than the buffer itself [{prevSize}-{buffer.GetBufferSize()}={prevSize - buffer.GetBufferSize()}]";
      return false;
    }

    if (byteSize > buffer.GetBufferSize()) {
      reason = $"byte size is larger than the buffer itself [{byteSize}-{buffer.GetBufferSize()}={byteSize - buffer.GetBufferSize()}]";
      return false;
    }

    var stagingBuffer = new DwarfBuffer(
      _allocator,
      _device,
      sumSize,
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
      stagingBuffer: true,
      cpuAccessible: true
    );

    stagingBuffer.Map();
    stagingBuffer.WriteToBuffer(data, byteSize);
    stagingBuffer.Unmap();

    var newBuffer = new DwarfBuffer(
      _allocator,
      _device,
      sumSize,
      BufferUsage.TransferDst | BufferUsage.VertexBuffer,
      MemoryProperty.DeviceLocal
    );

    _device.CopyBuffer(
      srcBuffer: buffer.GetBuffer(),
      dstBuffer: newBuffer.GetBuffer(),
      size: prevSize,
      srcOffset: 0,
      dstOffset: 0
    );
    _device.CopyBuffer(
      srcBuffer: stagingBuffer.GetBuffer(),
      dstBuffer: newBuffer.GetBuffer(),
      size: byteSize,
      srcOffset: 0,
      dstOffset: prevSize
    );
    stagingBuffer.Dispose();
    buffer.Dispose();
    _buffers[index].VertexBuffer = newBuffer;

    return true;
  }

  private unsafe bool AddToExistingBuffer(ref DwarfBuffer buffer, in nint data, in ulong byteSize, in ulong prevSize, ref string reason) {
    var sumSize = prevSize + byteSize;
    if (sumSize >= _maxBufferSize) {
      reason = $"sumSize of [{sumSize}] bytes is too large compared to maximum [{_maxBufferSize}] bytes";
      return false;
    }

    if (prevSize > buffer.GetBufferSize()) {
      reason = $"offset size is larger than the buffer itself [{prevSize}-{buffer.GetBufferSize()}={prevSize - buffer.GetBufferSize()}]";
      return false;
    }

    if (byteSize > buffer.GetBufferSize()) {
      reason = $"byte size is larger than the buffer itself [{byteSize}-{buffer.GetBufferSize()}={byteSize - buffer.GetBufferSize()}]";
      return false;
    }

    buffer.Map(byteSize, prevSize);
    buffer.WriteToBuffer(data, byteSize, prevSize);
    buffer.Unmap();

    reason = "Success";
    return true;
  }

  public int AddToPool() {
    var key = (uint)_buffers.Keys.Count;
    var addResult = _buffers.TryAdd(key, new());
    if (addResult) {
      return (int)key;
    } else {
      return -1;
    }
  }

  public void Flush() {
    foreach (var buff in _buffers) {
      buff.Value.VertexBuffer.Flush();
    }
  }

  public DwarfBuffer GetVertexBuffer(uint index) => _buffers[index].VertexBuffer;
  public DwarfBuffer GetIndexBuffer(uint index) => _buffers[index].IndexBuffer;

  public void Dispose() {
    Logger.Info($"[Buffer Pool] Disposing {_buffers.Count} pools");
    foreach (var buff in _buffers.Values) {
      buff?.VertexBuffer?.Dispose();
      buff?.IndexBuffer?.Dispose();
    }
    _buffers.Clear();
    // GC.SuppressFinalize(this);
  }
}