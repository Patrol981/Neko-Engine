using System.Numerics;

using Neko;
using Neko.AbstractionLayer;
using Neko.Rendering.UI;

using ImGuiNET;

namespace Neko.Rendering.UI.DirectRPG;

public partial class DirectRPG {
  public delegate void ButtonClickedDelegate();

  private static Vector2 s_canvasSize = Vector2.Zero;

  private static ImGuiWindowFlags s_subWindowFlags = ImGuiWindowFlags.ChildWindow;
  private static ImGuiChildFlags s_subWidnowChildFlags = ImGuiChildFlags.None;

  public static void BeginCanvas() {
    var io = ImGui.GetIO();
    s_canvasSize = io.DisplaySize;

    ImGui.SetNextWindowPos(new(0, 0));
    ImGui.SetNextWindowSize(io.DisplaySize);
    ImGui.SetNextWindowBgAlpha(0.0f);
    ImGui.Begin("Canvas",
      ImGuiWindowFlags.NoDecoration |
      ImGuiWindowFlags.NoMove |
      ImGuiWindowFlags.NoResize |
      ImGuiWindowFlags.NoBringToFrontOnFocus |
      ImGuiWindowFlags.NoBackground |
      ImGuiWindowFlags.NoSavedSettings |
      ImGuiWindowFlags.NoMouseInputs
    );
  }

  public static void EndCanvas() {
    ImGui.End();
  }

  public static void CanvasText(
    string text,
    Anchor anchor = Anchor.Middle,
    Vector2 offset = default
  ) {
    ValidateAnchor(text, offset, anchor);
    ImGui.Text(text);
  }

  public static void CanvasButton(string label, ButtonClickedDelegate buttonClicked, Anchor anchor = Anchor.Middle) {
    var size = ImGui.CalcTextSize(label) * 2;
    ValidateAnchor(size, anchor);
    ImGui.BeginChild("btn_child", size, s_subWidnowChildFlags, s_subWindowFlags);
    if (ImGui.Button(label, size)) {
      buttonClicked.Invoke();
    }
    ImGui.EndChild();
  }

  public static unsafe void CanvasImage(
    ref ITexture texture,
    Vector2 size,
    Anchor anchor = Anchor.Middle,
    Vector2 offset = default
  ) {
    ValidateAnchor(size + offset, anchor);
    var controller = Application.Instance.GuiController;
    var binding = controller.GetOrCreateImGuiBinding(texture);
    ImGui.Image(binding, size, new(0, 1), new(1, 0));
  }
}