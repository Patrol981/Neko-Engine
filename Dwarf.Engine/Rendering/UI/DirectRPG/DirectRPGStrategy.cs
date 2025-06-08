using System.Diagnostics;
using System.Numerics;
using Dwarf.AbstractionLayer;
using ImGuiNET;

namespace Dwarf.Rendering.UI.DirectRPG;

public partial class DirectRPG {
  private static float s_RTS_Rows = 0.0f;
  private static float s_RTS_Cols = 0.0f;

  private static ITexture? s_MainBg;
  private static ITexture? s_RTSAtlas;

  public static void CreateRTSTheme(Application app, ref ITexture atlasTexture) {
    Application.Mutex.WaitOne();
    s_MainBg = CreateTexture(app, "./Resources/UI/Banners/Carved_9Slides.png");
    s_RTSAtlas = atlasTexture;
    Application.Mutex.ReleaseMutex();
  }

  public static void CreateBottomRTSPanel() {
    var size = new Vector2(DisplaySize.X, 200);

    ImGui.SetNextWindowSize(size);
    ImGui.SetNextWindowPos(new(0, DisplaySize.Y - size.Y));
    ImGui.Begin("RTS_Panel", ImGuiWindowFlags.NoDecoration |
                             ImGuiWindowFlags.NoBringToFrontOnFocus |
                             ImGuiWindowFlags.NoMove
    );

    if (s_MainBg == null) return;

    // CreateTexturedPanel(s_MainBg, size);
  }

  public static void EndBottomRTSPanel() {
    ImGui.End();
  }

  public static void CreateRightRTSPanel() {
    var size = new Vector2(300, DisplaySize.Y);
    PreviousParentSize = size;

    ImGui.SetNextWindowSize(size);
    ImGui.SetNextWindowPos(new(DisplaySize.X - size.X, 0));
    ImGui.Begin("RTS_Panel", ImGuiWindowFlags.NoDecoration |
                             ImGuiWindowFlags.NoBringToFrontOnFocus |
                             ImGuiWindowFlags.NoMove
    );

    if (s_MainBg == null) return;
  }

  public static void EndRightRTSPanel() {
    ImGui.End();
  }

  public static void CreateGrid(PanelGridItem[,] items) {
    Debug.Assert(s_RTSAtlas != null);
    var size = new Vector2(80, 80);
    var sizeOffsetX = (PreviousParentSize.X - (size.X * items.GetLength(0))) / 2;
    var pos = ImGui.GetCursorScreenPos();

    pos.X += sizeOffsetX;
    pos.X -= (2.5f * items.GetLength(0));

    ImGui.SetCursorScreenPos(pos);
    ImGui.BeginChild("grid");
    for (int y = 0; y < items.GetLength(1); y++) {
      for (int x = 0; x < items.GetLength(0); x++) {
        var innerPos = ImGui.GetCursorScreenPos();
        GetUVCoords(
          items[x, y].TextureIndex,
          16,
          16,
          out var min,
          out var max
        );
        // ImGui.SetCursorScreenPos(ImGui.GetCursorScreenPos() + size);
        CreateTexturedButton(
          $"grid_btn_{x}_{y}",
          s_RTSAtlas,
          s_RTSAtlas,
          size,
          size,
          min,
          max,
          items[x, y].OnClickEvent
        );
        ImGui.SameLine();
        ImGui.SetCursorScreenPos(new Vector2(ImGui.GetCursorScreenPos().X, innerPos.Y));
      }

      ImGui.NewLine();
    }
    ImGui.EndChild();
  }

  public static void CreateEmptyGrid(int in_x, int in_y, out PanelGridItem[,] items) {
    items = new PanelGridItem[in_x, in_y];
    for (int y = 0; y < in_y; y++) {
      for (int x = 0; x < in_x; x++) {
        items[x, y] = new() {
          TextureIndex = 1,
          OnClickEvent = () => { },
          HoverText = ""
        };
      }
    }
  }
}