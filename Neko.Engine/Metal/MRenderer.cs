using Dwarf.AbstractionLayer;
using Dwarf.Math;
using Dwarf.Rendering;

namespace Dwarf.Metal;

public class MRenderer : IRenderer {
  public MRenderer(Application app) {

  }
  public nint CurrentCommandBuffer => throw new NotImplementedException();

  public int FrameIndex => throw new NotImplementedException();

  public int ImageIndex => throw new NotImplementedException();

  public float AspectRatio => throw new NotImplementedException();

  public DwarfExtent2D Extent2D => throw new NotImplementedException();

  public int MAX_FRAMES_IN_FLIGHT => throw new NotImplementedException();

  public ISwapchain Swapchain => throw new NotImplementedException();

  public DwarfFormat DepthFormat => throw new NotImplementedException();

  public CommandList CommandList => throw new NotImplementedException();

  public ulong PostProcessDecriptor => throw new NotImplementedException();

  public ulong PreviousPostProcessDescriptor => throw new NotImplementedException();

  public nint BeginFrame(CommandBufferLevel level = CommandBufferLevel.Primary) {
    throw new NotImplementedException();
  }

  public void BeginRendering(nint commandBuffer) {
    throw new NotImplementedException();
  }

  public void CreateCommandBuffers(ulong commandPool, CommandBufferLevel level = CommandBufferLevel.Primary) {
    throw new NotImplementedException();
  }

  public void Dispose() {
    throw new NotImplementedException();
  }

  public void EndFrame() {
    throw new NotImplementedException();
  }

  public void EndRendering(nint commandBuffer) {
    throw new NotImplementedException();
  }

  public ulong GetPostProcessingPass() {
    throw new NotImplementedException();
  }

  public ulong GetSwapchainRenderPass() {
    throw new NotImplementedException();
  }

  public void RecreateSwapchain() {
    throw new NotImplementedException();
  }

  public void UpdateDescriptors() {
    throw new NotImplementedException();
  }
}