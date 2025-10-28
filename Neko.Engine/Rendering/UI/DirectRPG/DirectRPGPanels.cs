using System.Numerics;
using Neko.AbstractionLayer;
using ImGuiNET;

namespace Neko.Rendering.UI.DirectRPG;

public partial class DirectRPG {
  public struct PanelGridItem {
    public int TextureIndex;
    public string HoverText;
    public ButtonClickedDelegate OnClickEvent;
  }

  public struct IndexedImage {
    public int TextureIndex;
  }

  public static void CreateTexturedPanel(
    ITexture texture,
    Vector2 size
  ) {
    var texId = GetStoredTexture(texture);
    var pos = ImGui.GetCursorScreenPos();

    ImGui.GetWindowDrawList().AddImage(texId, pos, pos + size, Uv0, Uv1);
  }
}