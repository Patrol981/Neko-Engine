
using System.Numerics;
using ImGuiNET;

namespace Neko.Rendering.UI.DirectRPG;

public partial class DirectRPG {
  public static void DrawActionBar() {
    var drawList = ImGui.GetForegroundDrawList();
    var size = DirectRPG.DisplaySize;

    float radius = 80f;
    float thickness = 15f;
    float percentage = 0.75f; // 75% filled

    // ImGui.SetCursorPos(size / 2);
    Vector2 cursor = ImGui.GetCursorScreenPos();
    Vector2 center = cursor + new Vector2(100, 100);
    DrawCircularProgressBar(percentage, radius, thickness, center, drawList);
  }

  public static void DrawCircularProgressBar(
    float percentage,
    float radius,
    float thickness,
    Vector2 center,
    ImDrawListPtr? drawList
  ) {
    if (!drawList.HasValue) {
      drawList = ImGui.GetForegroundDrawList();
    }

    const int segments = 64;
    float startAngle = MathF.PI * 0.75f;
    float endAngle = MathF.PI * 2.25f;

    drawList.Value.PathArcTo(center, radius, startAngle, endAngle, segments);
    drawList.Value.PathStroke(COLOR_BLACK, ImDrawFlags.None, thickness);

    float filledAngle = startAngle + (endAngle - startAngle) * percentage;
    drawList.Value.PathArcTo(center, radius, startAngle, filledAngle, segments);
    drawList.Value.PathStroke(COLOR_WHITE, ImDrawFlags.None, thickness);
  }
}