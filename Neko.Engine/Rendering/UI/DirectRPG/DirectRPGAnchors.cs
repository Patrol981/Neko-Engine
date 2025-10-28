using System.Numerics;

using Neko.Rendering.UI;

using ImGuiNET;

namespace Neko.Rendering.UI.DirectRPG;

public partial class DirectRPG {
  private static Vector2 ValidateAnchor(string text, Anchor anchor) {
    var textSize = ImGui.CalcTextSize(text);
    ValidateAnchor(textSize, anchor);
    return textSize;
  }

  private static void ValidateAnchor(string text, Vector2 offset, Anchor anchor) {
    var textSize = ImGui.CalcTextSize(text);
    ValidateAnchor(textSize + offset, anchor);
  }

  private static void ValidateAnchor(Vector2 size, Anchor anchor, bool stick = false) {
    var io = ImGui.GetIO();

    switch (anchor) {
      case Anchor.Right:
        ImGui.SetCursorPos(GetMiddleRight(io, size, stick));
        break;
      case Anchor.Left:
        ImGui.SetCursorPos(GetMiddleLeft(io, size, stick));
        break;
      case Anchor.Middle:
        ImGui.SetCursorPos(GetCenter(io, size, stick));
        break;
      case Anchor.Bottom:
        ImGui.SetCursorPos(GetMiddleBottom(io, size, stick));
        break;
      case Anchor.Top:
        ImGui.SetCursorPos(GetMiddleTop(io, size, stick));
        break;
      case Anchor.RightTop:
        ImGui.SetCursorPos(GetRightTop(io, size, stick));
        break;
      case Anchor.RightBottom:
        ImGui.SetCursorPos(GetRightBottom(io, size, stick));
        break;
      case Anchor.LeftTop:
        ImGui.SetCursorPos(GetLeftTop(io, size, stick));
        break;
      case Anchor.LeftBottom:
        ImGui.SetCursorPos(GetLeftBottom(io, size, stick));
        break;
      case Anchor.MiddleTop:
        ImGui.SetCursorPos(GetMiddleTop(io, size, stick));
        break;
      case Anchor.MiddleBottom:
        ImGui.SetCursorPos(GetMiddleBottom(io, size, stick));
        break;
      default:
        break;
    }
  }

  private static void SetWindowAlignment(Vector2 size, Anchor anchor, bool stick) {
    var io = ImGui.GetIO();

    switch (anchor) {
      case Anchor.Right:
        ImGui.SetNextWindowPos(GetMiddleRight(io, size, stick));
        break;
      case Anchor.Left:
        ImGui.SetNextWindowPos(GetMiddleLeft(io, size, stick));
        break;
      case Anchor.Middle:
        ImGui.SetNextWindowPos(GetCenter(io, size, stick));
        break;
      case Anchor.Bottom:
        ImGui.SetNextWindowPos(GetMiddleBottom(io, size, stick));
        break;
      case Anchor.Top:
        ImGui.SetNextWindowPos(GetMiddleTop(io, size, stick));
        break;
      case Anchor.RightTop:
        ImGui.SetNextWindowPos(GetRightTop(io, size, stick));
        break;
      case Anchor.RightBottom:
        ImGui.SetNextWindowPos(GetRightBottom(io, size, stick));
        break;
      case Anchor.LeftTop:
        ImGui.SetNextWindowPos(GetLeftTop(io, size, stick));
        break;
      case Anchor.LeftBottom:
        ImGui.SetNextWindowPos(GetLeftBottom(io, size, stick));
        break;
      case Anchor.MiddleTop:
        ImGui.SetNextWindowPos(GetMiddleTop(io, size, stick));
        break;
      case Anchor.MiddleBottom:
        ImGui.SetNextWindowPos(GetMiddleBottom(io, size, stick));
        break;
      default:
        break;
    }
  }

  private static Vector2 GetCenter(ImGuiIOPtr io, Vector2 offset, bool stick) {
    var displaySize = (io.DisplaySize / 2) - (offset / 2);
    return displaySize;
  }

  private static Vector2 GetMiddleBottom(ImGuiIOPtr io, Vector2 offset, bool stick) {
    var displaySize = io.DisplaySize;
    displaySize.X = (displaySize.X / 2) - (offset.X / 2);
    displaySize.Y = stick ? displaySize.Y - offset.Y / 2 : (displaySize.Y) - (offset.Y + 5);
    // displaySize.Y = (displaySize.Y) - (offset.Y + 5);
    return displaySize;
  }

  private static Vector2 GetMiddleTop(ImGuiIOPtr io, Vector2 offset, bool stick) {
    var displaySize = io.DisplaySize;
    displaySize.X = (displaySize.X / 2) - (offset.X / 2);
    displaySize.Y = (offset.Y / 2);
    return displaySize;
  }

  private static Vector2 GetLeftBottom(ImGuiIOPtr io, Vector2 offset, bool stick) {
    var displaySize = io.DisplaySize;
    displaySize.X = 5;
    displaySize.Y = (displaySize.Y) - (offset.Y + 5);
    return displaySize;
  }

  private static Vector2 GetRightBottom(ImGuiIOPtr io, Vector2 offset, bool stick) {
    var displaySize = io.DisplaySize;
    displaySize.X = (displaySize.X) - (offset.X + 5);
    displaySize.Y = (displaySize.Y) - (offset.Y + 5);
    return displaySize;
  }

  private static Vector2 GetRightTop(ImGuiIOPtr io, Vector2 offset, bool stick) {
    var displaySize = io.DisplaySize;
    displaySize.X = (displaySize.X) - (offset.X + 5);
    displaySize.Y = (offset.Y - 5);
    return displaySize;
  }

  private static Vector2 GetLeftTop(ImGuiIOPtr io, Vector2 offset, bool stick) {
    var displaySize = io.DisplaySize;
    displaySize.X = 5;
    displaySize.Y = (offset.Y / 2);
    return displaySize;
  }

  private static Vector2 GetMiddleLeft(ImGuiIOPtr io, Vector2 offset, bool stick) {
    var displaySize = io.DisplaySize;
    displaySize.X = 5;
    displaySize.Y = (displaySize.Y / 2) - (offset.Y / 2);
    return displaySize;
  }

  private static Vector2 GetMiddleRight(ImGuiIOPtr io, Vector2 offset, bool stick) {
    var displaySize = io.DisplaySize;
    displaySize.X = (displaySize.X) - (offset.X + 5);
    displaySize.Y = (displaySize.Y / 2) - (offset.Y / 2);
    return displaySize;
  }
}