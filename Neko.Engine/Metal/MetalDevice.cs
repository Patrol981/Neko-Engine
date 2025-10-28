using Neko.AbstractionLayer;
using Vortice.Vulkan;

namespace Neko.Metal;

public class MetalDevice : IDevice {
  public RenderAPI RenderAPI => RenderAPI.Metal;
  public MetalDevice() {

  }

  public ulong CommandPool => throw new NotImplementedException();

  public nint LogicalDevice => throw new NotImplementedException();

  public nint PhysicalDevice => throw new NotImplementedException();

  public nint GraphicsQueue => throw new NotImplementedException();

  public nint PresentQueue => throw new NotImplementedException();

  public ulong MinStorageBufferOffsetAlignment => throw new NotImplementedException();

  public ulong MinUniformBufferOffsetAlignment => throw new NotImplementedException();

  public ulong MaxBufferSize => throw new NotImplementedException();

  public ulong MaxHeapSize => throw new NotImplementedException();

  public void AllocateBuffer(ulong size, BufferUsage uFlags, MemoryProperty pFlags, ulong buffer, out ulong bufferMemory) {
    throw new NotImplementedException();
  }

  public nint BeginSingleTimeCommands() {
    throw new NotImplementedException();
  }

  public void BeginWaitFence(object fence, bool waitAll) {
    throw new NotImplementedException();
  }

  public Task CopyBuffer(ulong srcBuffer, ulong dstBuffer, ulong size) {
    throw new NotImplementedException();
  }

  public void CreateBuffer(ulong size, BufferUsage uFlags, MemoryProperty pFlags, out ulong buffer, out ulong bufferMemory) {
    throw new NotImplementedException();
  }

  public ulong CreateCommandPool() {
    throw new NotImplementedException();
  }

  public object CreateFence(FenceCreateFlags fenceCreateFlags) {
    throw new NotImplementedException();
  }

  public void Dispose() {
    throw new NotImplementedException();
  }

  public void EndSingleTimeCommands(nint commandBuffer) {
    throw new NotImplementedException();
  }

  public void EndWaitFence(object fence) {
    throw new NotImplementedException();
  }

  public uint FindMemoryType(uint typeFilter, MemoryProperty properties) {
    throw new NotImplementedException();
  }

  public void WaitAllQueues() {
    throw new NotImplementedException();
  }

  public void WaitDevice() {
    throw new NotImplementedException();
  }

  public void WaitFence(object fence, bool waitAll) {
    throw new NotImplementedException();
  }

  public void WaitQueue() {
    throw new NotImplementedException();
  }

  public void DisposeCommandPool(ulong commandPool) {
    throw new NotImplementedException();
  }

  public Task CopyBuffer(ulong srcBuffer, ulong dstBuffer, ulong size, ulong srcOffset, ulong dstOffset) {
    throw new NotImplementedException();
  }
}