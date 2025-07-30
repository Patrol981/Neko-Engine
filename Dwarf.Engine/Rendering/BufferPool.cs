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
  private const ulong MAX_BUFF_SIZE = 2500000;
  private const ulong MAX_HEAP_SIZE = 268435456;
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
      return AddToExistingBuffer(ref buffer.VertexBuffer, data, byteSize, prevSize, ref reason);
    }
  }

  public unsafe bool AddToIndex(uint index, uint[] data, out ulong offset, out string reason) {
    offset = 0;
    reason = "";
    if (!_buffers.ContainsKey(index)) return false;

    _buffers.TryGetValue(index, out var buffer);

    if (buffer!.IndexBuffer == null) {
      return CreateNewIndexBuffer(index, data);
    } else {
      fixed (uint* pData = data) {
        offset = buffer.IndexBuffer.GetBufferSize();
        return AddToExistingBuffer(ref buffer.IndexBuffer, (nint)pData, (ulong)data.Length * sizeof(uint), 0, ref reason);
      }
    }

  }

  private unsafe bool CreateNewIndexBuffer(in uint index, uint[] data) {
    var stagingBuffer = new DwarfBuffer(
      _allocator,
      _device,
      (ulong)data.Length * sizeof(uint),
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
      stagingBuffer: true,
      cpuAccessible: true
    );

    stagingBuffer.Map((ulong)data.Length * sizeof(uint));
    fixed (uint* pSrc = data.ToArray()) {
      stagingBuffer.WriteToBuffer((nint)pSrc, (ulong)data.Length * sizeof(uint));
    }
    stagingBuffer.Unmap();

    var indexBuffer = new DwarfBuffer(
      _allocator,
      _device,
      (ulong)Unsafe.SizeOf<uint>(),
      (uint)data.Length,
      BufferUsage.IndexBuffer | BufferUsage.TransferDst,
      MemoryProperty.DeviceLocal
    );

    _device.CopyBuffer(
      stagingBuffer.GetBuffer(),
      indexBuffer.GetBuffer(),
      (ulong)data.Length * sizeof(uint)
    );
    stagingBuffer.Dispose();

    _buffers[index].IndexBuffer = indexBuffer;

    return true;
  }

  private bool CreateNewVertexBuffer(in uint index, nint data, ulong byteSize) {
    if (byteSize > _maxBufferSize) {
      throw new ArgumentOutOfRangeException(nameof(byteSize), $"input is larger than max size by {byteSize - MAX_BUFF_SIZE} bytes");
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
      MAX_HEAP_SIZE,
      BufferUsage.TransferDst | BufferUsage.VertexBuffer,
      MemoryProperty.DeviceLocal
    );
    _device.CopyBuffer(stagingBuffer.GetBuffer(), newBuffer.GetBuffer(), stagingBuffer.GetBufferSize());
    stagingBuffer.Dispose();
    _buffers[index].VertexBuffer = newBuffer;

    return true;
  }

  private unsafe bool AddToExistingBuffer(ref DwarfBuffer buffer, in nint data, in ulong byteSize, in ulong prevSize, ref string reason) {
    var sumSize = prevSize + byteSize;
    if (sumSize >= MAX_HEAP_SIZE) {
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

  public DwarfBuffer GetVertexBuffer(uint index) => _buffers[index].VertexBuffer;
  public DwarfBuffer GetIndexBuffer(uint index) => _buffers[index].IndexBuffer;

  public void Dispose() {
    Logger.Info($"[Buffer Pool] Disposing {_buffers.Count} pools");
    foreach (var buff in _buffers.Values) {
      buff.VertexBuffer.Dispose();
      buff.IndexBuffer.Dispose();
    }
    _buffers.Clear();
    GC.SuppressFinalize(this);
  }
}