namespace Dwarf.Rendering;

public interface IDrawable : IDisposable {
  public Task Bind(IntPtr commandBuffer, uint index);
  // public void Bind(VkCommandBuffer commandBuffer);
  public Task Draw(IntPtr commandBuffer, uint index = 0, uint firstInstance = 0);
}
