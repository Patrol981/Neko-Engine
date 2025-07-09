using Dwarf.AbstractionLayer;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Vulkan;

public class VulkanCommandList : CommandList {
  public override void BindVertex(
    nint commandBuffer,
    uint meshIndex,
    DwarfBuffer[] vertexBuffers,
    ulong[] vertexOffsets
  ) {
    VkBuffer[] vBuffers = [vertexBuffers[meshIndex].GetBuffer()];
    unsafe {
      fixed (VkBuffer* buffersPtr = vBuffers)
      fixed (ulong* offsetsPtr = vertexOffsets) {
        vkCmdBindVertexBuffers(commandBuffer, 0, 1, buffersPtr, offsetsPtr);
      }
    }
  }

  public override void BindVertex(
    IntPtr commandBuffer,
    DwarfBuffer vertexBuffer,
    ulong vertexOffset
  ) {
    unsafe {
      VkBuffer[] vBuffers = [vertexBuffer.GetBuffer()];
      ulong[] offsets = [vertexOffset];
      fixed (VkBuffer* buffersPtr = vBuffers)
      fixed (ulong* offsetsPtr = offsets) {
        vkCmdBindVertexBuffers(commandBuffer, 0, 1, buffersPtr, offsetsPtr);
      }
    }
  }

  public override void BindIndex(nint commandBuffer, uint meshIndex, DwarfBuffer[] indexBuffers, ulong offset = 0) {
    vkCmdBindIndexBuffer(commandBuffer, indexBuffers[meshIndex].GetBuffer(), offset, VkIndexType.Uint32);
  }

  public override void BindIndex(nint commandBuffer, DwarfBuffer indexBuffer, ulong offset = 0) {
    vkCmdBindIndexBuffer(commandBuffer, indexBuffer.GetBuffer(), offset, VkIndexType.Uint32);
  }

  public override void Draw(
    nint commandBuffer,
    uint meshIndex,
    ulong[] vertexCount,
    uint instanceCount,
    uint firstVertex,
    uint firstInstance
  ) {
    vkCmdDraw(commandBuffer, (uint)vertexCount[meshIndex], instanceCount, firstVertex, firstInstance);
  }

  public override void Draw(
    nint commandBuffer,
    ulong vertexCount,
    uint instanceCount,
    uint firstVertex,
    uint firstInstance
  ) {
    vkCmdDraw(commandBuffer, (uint)vertexCount, instanceCount, firstVertex, firstInstance);
  }

  public override void DrawIndirect(
    nint commandBuffer,
    ulong indirectBuffer,
    ulong offset,
    uint drawCount,
    uint stride
  ) {
    vkCmdDrawIndirect(commandBuffer, indirectBuffer, offset, drawCount, stride);
  }

  public override void DrawIndexed(
    nint commandBuffer,
    uint meshIndex,
    ulong[] indexCount,
    uint instanceCount,
    uint firstIndex,
    int vertexOffset,
    uint firstInstance
  ) {
    vkCmdDrawIndexed(commandBuffer, (uint)indexCount[meshIndex], instanceCount, firstIndex, vertexOffset, firstInstance);
  }

  public override void DrawIndexed(
    nint commandBuffer,
    ulong indexCount,
    uint instanceCount,
    uint firstIndex,
    int vertexOffset,
    uint firstInstance
  ) {
    vkCmdDrawIndexed(commandBuffer, (uint)indexCount, instanceCount, firstIndex, vertexOffset, firstInstance);
  }

  public override void DrawIndexedIndirect(
    nint commandBuffer,
    ulong indirectBuffer,
    ulong offset,
    uint drawCount,
    uint stride
  ) {
    vkCmdDrawIndexedIndirect(commandBuffer, indirectBuffer, offset, drawCount, stride);
  }

  public override void SetViewport(
    nint commandBuffer,
    float x, float y,
    float width, float height,
    float minDepth, float maxDepth
  ) {
    var viewport = VkUtils.Viewport(x, y, width, height, minDepth, maxDepth);
    vkCmdSetViewport(commandBuffer, viewport);
  }
}
