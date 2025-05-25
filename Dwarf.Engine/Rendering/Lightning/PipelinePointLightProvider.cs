using Dwarf.Vulkan;

using Vortice.Vulkan;

namespace Dwarf.Rendering.Lightning;

public class PipelinePointLightProvider : VkPipelineProvider {
  public override unsafe VkVertexInputBindingDescription* GetBindingDescsFunc() {
    return null;

    /*
    var bindingDescriptions = new VkVertexInputBindingDescription[1];
    fixed (VkVertexInputBindingDescription* ptr = bindingDescriptions) {
      return ptr;
    }
    */
  }

  public override unsafe VkVertexInputAttributeDescription* GetAttribDescsFunc() {
    return null;
    /*
    var attributeDescriptions = new VkVertexInputAttributeDescription[GetAttribsLength()];

    fixed (VkVertexInputAttributeDescription* ptr = attributeDescriptions) {
      return ptr;
    }
    */
  }

  public override uint GetAttribsLength() {
    return 0;
  }

  public override uint GetBindingsLength() {
    return 0;
  }
}
