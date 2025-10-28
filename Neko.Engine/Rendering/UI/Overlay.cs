using System.Numerics;

using Neko.Globals;

using ImGuiNET;

namespace Neko.Rendering.UI;

public static class Overlay {
  private static Vector2 s_windowPos = new();
  private static Vector2 s_windowPosPivot = new();
  private static float s_updateDelay = 0.0f;
  private static string s_framerate = "";

  public static void BeginAndEndOverlay() {
    var io = ImGui.GetIO();

    if (s_updateDelay > 1.0f) {
      s_updateDelay = 0.0f;
      s_framerate = io.Framerate.ToString();
    }
    s_updateDelay += Time.DeltaTime;

    var windowFlags = ImGuiWindowFlags.NoDecoration |
                      ImGuiWindowFlags.AlwaysAutoResize |
                      ImGuiWindowFlags.NoSavedSettings |
                      ImGuiWindowFlags.NoFocusOnAppearing |
                      ImGuiWindowFlags.NoNav;

    var pad = 10.0f;
    var mainViewport = ImGui.GetMainViewport();
    var workPos = mainViewport.WorkPos;
    var workSize = mainViewport.WorkSize;

    s_windowPos.X = workPos.X + workSize.X - pad;
    s_windowPos.Y = workPos.Y + workSize.Y - pad;
    s_windowPosPivot.X = 1.0f;
    s_windowPosPivot.Y = 1.0f;
    ImGui.SetNextWindowPos(s_windowPos, ImGuiCond.Always, s_windowPosPivot);

    if (ImGui.Begin("Overlay", windowFlags)) {
      ImGui.Text("Overlay");
      ImGui.Separator();
      ImGui.Text(Time.DeltaTime.ToString());
    }
    ImGui.End();
  }
}
