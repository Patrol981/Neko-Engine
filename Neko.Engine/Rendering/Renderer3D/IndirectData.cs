using Vortice.Vulkan;

namespace Neko.Rendering.Renderer3D;

public sealed class IndirectData {
  public readonly List<VkDrawIndexedIndirectCommand> Commands = [];
  public int VisibleCount;
  public uint CurrentIndexOffset;
}

public readonly struct CmdRef(uint pool, int cmdIndex) {
  public readonly uint Pool = pool;
  public readonly int CmdIndex = cmdIndex;
}