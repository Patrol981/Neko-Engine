using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Dwarf.Vulkan;

using Vortice.Vulkan;

namespace Dwarf.Rendering.PostProcessing;

public class PostProcessingPipelineProvider : VkPipelineProvider {
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
