using System.Numerics;
using System.Runtime.Intrinsics.X86;
using Neko.AbstractionLayer;
using ImGuiNET;

namespace Neko.Rendering.UI.DirectRPG;

public partial class DirectRPG {
  private static float s_menuOffset = 0;
  private static float s_startOffsetY = 0;
  private static MenuConfig s_menuConfig;

  public struct MenuConfig {
    public bool HasBackground;
    public Vector3 BackgroundColor;
    public ITexture BackgroundTexture;

    public Vector2 Size;

    public float WindowAlpha;

    public Anchor Anchor;
  }

  public static void BeginMenu(MenuConfig menuConfig) {
    s_menuConfig = menuConfig;
    CreateMenuStyles();
    var io = ImGui.GetIO();
    // ImGui.SetNextWindowPos(new(0, 0));

    switch (menuConfig.Anchor) {
      case Anchor.Left:
        ImGui.SetNextWindowPos(new(0, 0));
        break;
      case Anchor.Middle:
        var midPos = new Vector2(DisplaySize.X / 2, DisplaySize.Y / 2);
        midPos.X -= MenuSize.X / 2;
        midPos.Y -= MenuSize.Y / 2;
        ImGui.SetNextWindowPos(midPos);
        break;
      case Anchor.Right:
        break;
    }

    ImGui.SetNextWindowSize(MenuSize);
    ImGui.SetNextWindowBgAlpha(s_menuConfig.WindowAlpha);
    ImGui.Begin("Fullscreen Menu",
      ImGuiWindowFlags.NoDecoration |
      ImGuiWindowFlags.NoMove |
      ImGuiWindowFlags.NoResize |
      ImGuiWindowFlags.NoBringToFrontOnFocus
    );
    if (s_menuConfig.BackgroundTexture != null) {
      var controller = Application.Instance.GuiController;
      var binding = controller.GetOrCreateImGuiBinding(s_menuConfig.BackgroundTexture);
      // ImGui.Image(binding, ImGui.GetWindowSize(), s_uv0, s_uv1);
      DirectRPG.Image(s_menuConfig.BackgroundTexture);
    }
  }

  public static void EndMenu() {
    ImGui.End();
    s_menuOffset = 0;
    s_startOffsetY = 0;
  }
  public static Vector2 MenuSize => s_menuConfig.Size;

  public static void SetOffsetY(float value) {
    s_startOffsetY = value;
  }

  public static void CreateMenuButton(
    string label,
    ButtonClickedDelegate onClick,
    Anchor anchor,
    Vector2 size = default,
    ITexture? bgTexture = null
  ) {
    var io = ImGui.GetIO();

    if (size == default) {
      size.X = 200;
      size.Y = 50;
    }

    switch (anchor) {
      case Anchor.Middle:
        // CreateMenuButton(label, onClick, size, true);
        var midPos = new Vector2(MenuSize.X / 2, s_startOffsetY);
        midPos.X -= size.X / 2;
        midPos.Y -= (size.Y / 2) - s_menuOffset * 2;
        ImGui.SetCursorPos(midPos);
        break;
      case Anchor.Left:
        var leftPos = new Vector2(size.X, s_startOffsetY);
        leftPos.Y -= (size.Y / 2) - s_menuOffset * 2;
        ImGui.SetCursorPos(leftPos);
        break;
    }

    if (ImGui.Button(label, size)) {
      onClick.Invoke();
    }

    s_menuOffset += size.Y;
  }

  public static void CreateMenuButton(
    string label,
    ButtonClickedDelegate buttonClicked,
    Vector2 size = default,
    bool center = true
  ) {
    var io = ImGui.GetIO();

    if (size == default) {
      size.X = 200;
      size.Y = 50;
    }
    if (center) {
      var centerPos = io.DisplaySize / 2;
      centerPos.X -= size.X / 2;
      centerPos.Y -= (size.Y / 2) - s_menuOffset * 2;
      ImGui.SetCursorPos(centerPos);
    }
    if (ImGui.Button(label, size)) {
      buttonClicked.Invoke();
    }

    s_menuOffset += size.Y;
  }

  public static void CreateMenuText(string label, bool center = true) {
    var io = ImGui.GetIO();
    var len = ImGui.CalcTextSize(label);

    if (center) {
      var centerPos = io.DisplaySize / 2;
      centerPos.X -= len.X / 2;
      centerPos.Y -= (len.Y / 2) - s_menuOffset * 2;
      ImGui.SetCursorPos(centerPos);
    }
    ImGui.Text(label);

    s_menuOffset += len.Y * 2;
  }
}