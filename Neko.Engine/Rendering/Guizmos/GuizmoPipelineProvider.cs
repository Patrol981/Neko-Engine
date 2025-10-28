using Neko.Vulkan;

using Vortice.Vulkan;

namespace Neko.Rendering.Guizmos;

public class GuizmoPipelineProvider : VkPipelineProvider {
  public override unsafe VkVertexInputBindingDescription* GetBindingDescsFunc() {
    return null;
  }

  public override unsafe VkVertexInputAttributeDescription* GetAttribDescsFunc() {
    return null;
  }

  public override uint GetAttribsLength() {
    return 0;
  }

  public override uint GetBindingsLength() {
    return 0;
  }
}
