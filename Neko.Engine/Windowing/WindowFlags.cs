namespace Dwarf.Windowing;

[Flags]
public enum WindowFlags {
  None = 0,
  Fullscreen = 1 << 0,
  Borderless = 1 << 1,
  Resizable = 1 << 2,
  Minimized = 1 << 3,
  Maximized = 1 << 4,
}