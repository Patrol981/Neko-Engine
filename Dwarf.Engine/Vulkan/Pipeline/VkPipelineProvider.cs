using Vortice.Vulkan;

namespace Dwarf.Vulkan;

public abstract class VkPipelineProvider {
  public virtual unsafe VkVertexInputBindingDescription* GetBindingDescsFunc() {
    throw new EntryPointNotFoundException("Cannot load non overrided function");
  }

  public virtual unsafe VkVertexInputAttributeDescription* GetAttribDescsFunc() {
    throw new EntryPointNotFoundException("Cannot load non overrided function");
  }

  public virtual uint GetBindingsLength() {
    throw new EntryPointNotFoundException("Cannot load non overrided function");
  }

  public virtual uint GetAttribsLength() {
    throw new EntryPointNotFoundException("Cannot load non overrided function");
  }
}
