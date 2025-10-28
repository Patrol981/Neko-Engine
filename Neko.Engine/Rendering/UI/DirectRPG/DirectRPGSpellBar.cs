using System.Numerics;
using Neko.Extensions.Logging;
using ImGuiNET;

namespace Neko.Rendering.UI.DirectRPG;

public delegate void SpellCallback();

public struct SpellBarItem {
  public int TextureIndex;
  public SpellCallback OnClick;
  public SpellCallback OnHover;
}

public partial class DirectRPG {
  private const int SPELL_BAR_ITEMS_PER_ROW_BASE = 5;
  private const int SPELL_BAR_SIZE_BASE_X = 470;
  private const int SPELL_BAR_SIZE_BASE_Y = 95;
  private const int SPELL_ITEM_SIZE_BASE_X = 47;
  private const int SPELL_ITEM_SIZE_BASE_Y = 95;

  public static float SpellbarScale = 1.5f;
  public static Vector2 SpellBarSize = new(SPELL_BAR_SIZE_BASE_X, SPELL_BAR_SIZE_BASE_Y);

  private static int s_spellBarSlotLength = 40;
  private static int s_spellBarItemsPerRow = SPELL_BAR_ITEMS_PER_ROW_BASE;
  private static Vector2 s_spellItemSize = new(36, 36);
  private static SpellBarItem[] s_spellBarItems = new SpellBarItem[s_spellBarSlotLength];

  private static string s_textureAtlasPath = string.Empty;
  private static Vector2 s_uvMin = new(0, 0);
  private static Vector2 s_uvMax = new(0, 0);
  private static int s_texturesPerRow = 0;

  public static async void SetSpellItems(SpellBarItem[] spellBarItems) {
    var rowsApprox = MathF.Ceiling(spellBarItems.Length / 10f);
    s_spellBarSlotLength = (int)rowsApprox * 10;
    s_spellBarItems = new SpellBarItem[s_spellBarSlotLength];

    ImGui.GetStyle().ItemInnerSpacing = new(1f, 1f);
    ImGui.GetStyle().ItemSpacing = new(1.5f, 1.5f);

    for (int i = 0; i < s_spellBarSlotLength; i++) {
      s_spellBarItems[i] = spellBarItems.Length > i ? spellBarItems[i] : new SpellBarItem();
    }

    var app = Application.Instance;
    await app.TextureManager.AddTextureGlobal("./Resources/crawler_atlas.png", 1);
    var textureId = app.TextureManager.GetTextureIdGlobal("./Resources/crawler_atlas.png");
    var texture = app.TextureManager.GetTextureGlobal(textureId);
    UploadTexture(texture);
  }

  public static void SetTextureAtlas(string path, int offset, int itemsPerRow) {
    s_textureAtlasPath = path;
    s_texturesPerRow = itemsPerRow;
  }

  public static void CreateSpellBar() {
    var io = ImGui.GetIO();

    s_spellBarItemsPerRow = (int)MathF.Floor(io.DisplaySize.X / 150);
    var rowsApprox = MathF.Ceiling(s_spellBarItems.Length / s_spellBarItemsPerRow);
    SpellBarSize.X = SPELL_ITEM_SIZE_BASE_X * s_spellBarItemsPerRow;
    var sizeToUpdate = SPELL_ITEM_SIZE_BASE_Y * rowsApprox;
    SpellBarSize.Y = SPELL_BAR_SIZE_BASE_Y + sizeToUpdate;

    if (s_textureAtlasPath == string.Empty) return;

    ImGui.BeginChild("###SpellBar");
    int x = 0;

    for (int i = 0; i < s_spellBarSlotLength; i++) {
      nint imTex = GetStoredTexture("./Resources/crawler_atlas.png");

      GetUVCoords(
        s_spellBarItems[i].TextureIndex,
        s_texturesPerRow,
        s_texturesPerRow,
        out s_uvMin,
        out s_uvMax
      );

      if (ImGui.ImageButton($"{i}", imTex, s_spellItemSize, s_uvMin, s_uvMax)) {
        s_spellBarItems[i].OnClick?.Invoke();
      }

      if (ImGui.IsItemHovered()) {
        ImGui.BeginTooltip();
        s_spellBarItems[i].OnHover?.Invoke();
        ImGui.EndTooltip();
      }

      x++;
      if (x < s_spellBarItemsPerRow) {
        ImGui.SameLine();
      } else {
        x = 0;
      }
    }
    ImGui.EndChild();
  }
}
