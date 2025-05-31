namespace Dwarf.AbstractionLayer;

[Flags]
public enum FenceCreateFlags {
  None = 0,
  Signaled = 0x00000001,
}