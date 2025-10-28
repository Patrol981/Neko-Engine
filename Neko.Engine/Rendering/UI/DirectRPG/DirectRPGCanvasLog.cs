using System.Numerics;

using Neko.Rendering.UI;

using ImGuiNET;

namespace Neko.Rendering.UI.DirectRPG;

public partial class DirectRPG {
  private static IEnumerable<string> s_logMessages = [];

  public static void AddLog(string logMsg) {
    s_logMessages = s_logMessages.Append(logMsg);
  }

  public static void CanvasLog() {
    var size = s_canvasSize;
    size.X /= 4;
    size.Y /= 5;
    CanvasLogBase(size);
  }

  public static void CanvasLogNextToSpellBar() {
    var io = ImGui.GetIO();
    var xCenter = (int)io.DisplaySize.X >> 1;
    var xOffset = (int)SpellBarSize.X >> 1;
    var size = new Vector2(((xCenter) - xOffset) >> 1, 150);
    CanvasLogBase(size);
  }

  public static void CanvasLog(Vector2 size) {
    CanvasLogBase(size);
  }

  private static void CanvasLogBase(Vector2 size) {
    ImGui.SetNextWindowSize(size);
    size.X -= 6;
    size.Y -= 6;
    SetWindowAlignment(size, Anchor.RightBottom, false);
    ImGui.Begin("Canvas Log");
    using var seq = s_logMessages.GetEnumerator();
    while (seq.MoveNext()) {
      ImGui.TextWrapped(seq.Current);
    }
    ImGui.End();
  }
}
