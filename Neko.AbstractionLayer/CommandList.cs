namespace Neko.AbstractionLayer;

public abstract class CommandList {
  public abstract void BindVertex(
    IntPtr commandBuffer,
    uint index,
    NekoBuffer[] vertexBuffers,
    ulong[] vertexOffsets
  );

  public abstract void BindVertex(
    IntPtr commandBuffer,
    NekoBuffer vertexBuffer,
    ulong vertexOffset
  );

  public abstract void BindIndex(IntPtr commandBuffer, uint index, NekoBuffer[] indexBuffers, ulong offset = 0);

  public abstract void BindIndex(IntPtr commandBuffer, NekoBuffer indexBuffer, ulong offset = 0);

  public abstract void Draw(
    nint commandBuffer,
    uint meshIndex,
    ulong[] vertexCount,
    uint instanceCount,
    uint firstVertex,
    uint firstInstance
  );

  public abstract void Draw(
    nint commandBuffer,
    ulong vertexCount,
    uint instanceCount,
    uint firstVertex,
    uint firstInstance
  );

  public abstract void DrawIndirect(
    nint commandBuffer,
    ulong indirectBuffer,
    ulong offset,
    uint drawCount,
    uint stride
  );

  public abstract void DrawIndexed(
    nint commandBuffer,
    uint meshIndex,
    ulong[] indexCount,
    uint instanceCount,
    uint firstIndex,
    int vertexOffset,
    uint firstInstance
  );

  public abstract void DrawIndexed(
    nint commandBuffer,
    ulong indexCount,
    uint instanceCount,
    uint firstIndex,
    int vertexOffset,
    uint firstInstance
  );

  public abstract void DrawIndexedIndirect(
    nint commandBuffer,
    ulong indirectBuffer,
    ulong offset,
    uint drawCount,
    uint stride
  );

  public abstract void SetViewport(
    nint commandBuffer,
    float x, float y,
    float width, float height,
    float minDepth, float maxDepth
  );
}
