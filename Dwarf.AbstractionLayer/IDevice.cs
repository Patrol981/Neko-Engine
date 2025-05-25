using Vortice.Vulkan;

namespace Dwarf.AbstractionLayer;

public interface IDevice : IDisposable {
  public void CreateBuffer(
    ulong size,
    BufferUsage uFlags,
    MemoryProperty pFlags,
    out ulong buffer,
    out ulong bufferMemory
  );

  public unsafe void AllocateBuffer(
    ulong size,
    BufferUsage uFlags,
    MemoryProperty pFlags,
    ulong buffer,
    out ulong bufferMemory
  );

  public Task CopyBuffer(ulong srcBuffer, ulong dstBuffer, ulong size);

  public void WaitQueue();
  public void WaitAllQueues();
  public void WaitDevice();

  public object CreateFence(FenceCreateFlags fenceCreateFlags);
  public void WaitFence(object fence, bool waitAll);
  public void BeginWaitFence(object fence, bool waitAll);
  public void EndWaitFence(object fence);

  public nint BeginSingleTimeCommands();
  public void EndSingleTimeCommands(nint commandBuffer);
  public uint FindMemoryType(uint typeFilter, MemoryProperty properties);

  ulong CommandPool { get; }
  ulong CreateCommandPool();

  IntPtr LogicalDevice { get; }
  IntPtr PhysicalDevice { get; }

  nint GraphicsQueue { get; }
  nint PresentQueue { get; }

  ulong MinStorageBufferOffsetAlignment { get; }
  ulong MinUniformBufferOffsetAlignment { get; }

  RenderAPI RenderAPI { get; }
}