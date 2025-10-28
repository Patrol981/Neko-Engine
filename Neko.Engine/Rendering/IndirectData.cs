using Vortice.Vulkan;

namespace Neko.Rendering;

public class IndirectData {
  public List<VkDrawIndexedIndirectCommand> Commands = [];
  public uint CurrentIndexOffset = 0;
  public uint InstanceIndex = 0;
}