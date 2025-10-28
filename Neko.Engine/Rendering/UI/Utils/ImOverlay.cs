using System.Numerics;
using ImGuiNET;

namespace Neko.Rendering.UI.Utils;

public static class ImOverlay {
  public delegate void OverlayInvoker();

  private static ImGuiWindowFlags s_windowFlags = ImGuiWindowFlags.NoDecoration |
                                                  ImGuiWindowFlags.AlwaysAutoResize |
                                                  ImGuiWindowFlags.NoSavedSettings |
                                                  ImGuiWindowFlags.NoFocusOnAppearing |
                                                  ImGuiWindowFlags.NoNav;

  private static Vector2 s_windowPos = Vector2.Zero;
  private static Vector2 s_windowPosPivot = Vector2.Zero;
  private static Vector2 s_windowSize = new(300, 150);
  public static void DrawOverlay(string label, OverlayInvoker? overlayInvoker) {
    var viewport = ImGui.GetMainViewport();
    var workPos = viewport.WorkPos;
    var workSize = viewport.WorkSize;

    s_windowPos.X = workSize.X - s_windowSize.X;
    s_windowPos.Y = workPos.Y;

    ImGui.SetNextWindowPos(s_windowPos);
    ImGui.SetNextWindowSize(s_windowSize);

    if (ImGui.Begin(label, s_windowFlags)) {
      overlayInvoker?.Invoke();

      ImGui.End();
    }
  }
}