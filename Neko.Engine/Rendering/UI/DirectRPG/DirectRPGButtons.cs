using System.Numerics;
using Neko.AbstractionLayer;
using ImGuiNET;

namespace Neko.Rendering.UI.DirectRPG;

public partial class DirectRPG {
  public static void CreateTexturedButton(
    string buttonId,
    ITexture textureStandard,
    ITexture textureHovered,
    Vector2 size,
    Vector2 clickArea,
    ButtonClickedDelegate buttonClicked
  ) {
    var pos = ImGui.GetCursorScreenPos();

    var sizeOffset = (PreviousParentSize - size) / 2;
    pos.X += sizeOffset.X;

    ImGui.SetCursorScreenPos(pos);
    ImGui.InvisibleButton(buttonId, clickArea);

    if (ImGui.IsItemHovered()) {
      var texId = GetStoredTexture(textureHovered);
      ImGui.GetWindowDrawList().AddImage(texId, pos, pos + size, Uv0, Uv1);
    } else {
      var texId = GetStoredTexture(textureStandard);
      ImGui.GetWindowDrawList().AddImage(texId, pos, pos + size, Uv0, Uv1);
    }

    if (ImGui.IsItemClicked()) {
      buttonClicked.Invoke();
    }
  }

  public static void CreateTexturedButton(
    string buttonId,
    ITexture textureStandard,
    ITexture textureHovered,
    Vector2 size,
    Vector2 clickArea,
    Vector2 uv0,
    Vector2 uv1,
    ButtonClickedDelegate buttonClicked
  ) {
    var padding = new Vector2(1.25f, 1.25f);
    var pos = ImGui.GetCursorScreenPos();
    var paddedPos = pos + padding;
    var paddedClickArea = clickArea + padding * 2;

    ImGui.SetCursorScreenPos(paddedPos);
    ImGui.InvisibleButton(buttonId, paddedClickArea);

    var drawPosStart = paddedPos;
    var drawPosEnd = paddedPos + size;

    if (ImGui.IsItemHovered()) {
      var texId = GetStoredTexture(textureHovered);
      ImGui.GetWindowDrawList().AddImage(texId, drawPosStart, drawPosEnd, uv0, uv1);
    } else {
      var texId = GetStoredTexture(textureStandard);
      ImGui.GetWindowDrawList().AddImage(texId, drawPosStart, drawPosEnd, uv0, uv1);
    }

    if (ImGui.IsItemClicked()) {
      buttonClicked.Invoke();
    }

    ImGui.SetCursorScreenPos(pos + new Vector2(0, paddedClickArea.Y));
    ImGui.Dummy(size);
  }

  public static void CreateTexturedButtonWithLabel(
    string buttonId,
    string label,
    ITexture textureStandard,
    ITexture textureHovered,
    Vector2 size,
    Vector2 clickArea,
    float fontSize,
    ButtonClickedDelegate buttonClicked
  ) {
    var pos = ImGui.GetCursorScreenPos();

    var sizeOffset = (PreviousParentSize - size) / 2;
    pos.X += sizeOffset.X;

    ImGui.SetCursorScreenPos(pos);
    ImGui.InvisibleButton(buttonId, clickArea);

    var textLen = ImGui.CalcTextSize(label);
    var textPos = pos + (size - textLen) / 2;
    textPos.X -= fontSize / 2;
    var font = ImGui.GetFont();

    if (ImGui.IsItemHovered()) {
      var texId = GetStoredTexture(textureHovered);
      ImGui.GetWindowDrawList().AddImage(texId, pos, pos + size, Uv0, Uv1);
      textPos.Y -= fontSize / 2 - 5;
      ImGui.GetWindowDrawList().AddText(font, fontSize, textPos, COLOR_WHITE, label);
    } else {
      var texId = GetStoredTexture(textureStandard);
      ImGui.GetWindowDrawList().AddImage(texId, pos, pos + size, Uv0, Uv1);
      textPos.Y -= fontSize / 2;
      ImGui.GetWindowDrawList().AddText(font, fontSize, textPos, COLOR_BLACK, label);
    }

    if (ImGui.IsItemClicked()) {
      buttonClicked.Invoke();
    }

    // ImGui.SetCursorPos(pos + size);
  }
}