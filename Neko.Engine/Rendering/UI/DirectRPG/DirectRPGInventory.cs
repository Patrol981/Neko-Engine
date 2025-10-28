using System.Numerics;
using ImGuiNET;

namespace Neko.Rendering.UI.DirectRPG;

public struct InventoryItem {
  public string TextureId;
  public string ItemName;
  public string ItemDesc;
}

public partial class DirectRPG {
  private static int s_inventorySlotSize = 64;
  private static Vector2 s_inventoryIconSize = new(64, 64);
  private static int s_inventorySlotsPerRow = 7;

  private static InventoryItem[] s_items = [];

  public static void SetInventoryData(InventoryItem[] inventoryItems) {
    s_items = inventoryItems;
  }

  public static void CreateInventory() {
    var size = ImGui.GetWindowSize();
    s_inventorySlotsPerRow = (int)size.X / (s_inventorySlotSize + 10);

    ImGui.BeginChild("###Inventory");

    for (int i = 0; i < s_items.Length; i++) {
      if (i > 0 && s_inventorySlotsPerRow > 0) {
        if (i % s_inventorySlotsPerRow != 0) ImGui.SameLine();
      }

      var imTex = GetStoredTexture(s_items[i].TextureId);
      if (ImGui.ImageButton($"{i}", imTex, s_inventoryIconSize, new(0, 1), new(1, 0))) {

      }

      if (ImGui.IsItemHovered()) {
        ImGui.BeginTooltip();
        ImGui.Text(s_items[i].ItemName);
        ImGui.Text(s_items[i].ItemDesc);
        ImGui.EndTooltip();
      }
    }

    ImGui.EndChild();
  }
}