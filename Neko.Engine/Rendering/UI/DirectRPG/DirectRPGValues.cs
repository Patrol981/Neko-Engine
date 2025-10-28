using System.Numerics;
using ImGuiNET;

namespace Neko.Rendering.UI.DirectRPG;

public partial class DirectRPG {
  private static Vector2 s_uv0 = new(0, 1);
  private static Vector2 s_uv1 = new(1, 0);
  private static Vector4 s_bgCol4 = Vector4.Zero;
  private static Vector4 s_tintCol4 = Vector4.One;

  public static Vector2 DisplaySize => ImGui.GetIO().DisplaySize;
  public static ImGuiController GuiController => Application.Instance.GuiController;
  public static Vector2 Uv0 => s_uv0;
  public static Vector2 Uv1 => s_uv1;
  public static Vector4 BgColor4 => s_bgCol4;
  public static Vector4 TintColor4 => s_tintCol4;

  public static Vector2 PreviousChildSize { get; private set; }
  public static Vector2 PreviousParentSize { get; private set; }
}