using DotTiled;
using DotTiled.Serialization;
using Dwarf.EntityComponentSystem;
using Dwarf.Extensions.Logging;
using Dwarf.Rendering.Renderer2D;
using Dwarf.Rendering.Renderer2D.Components;
using Dwarf.Rendering.Renderer2D.Models;
using Dwarf.Utils;

namespace Dwarf.Loaders.Tiled;

public static class TiledLoader {
  public static Tilemap LoadTilemap(Entity owner, Application app, string tmxPath) {
    var loader = Loader.Default();
    var map = loader.LoadMap(Path.Combine(DwarfPath.AssemblyDirectory, tmxPath));

    if (map.Infinite) throw new NotSupportedException("Loader does not support infinite maps");

    List<string> tileSources = [];
    List<BackgroundData> bgSources = [];
    var tilemap = new Tilemap(owner, app, new((int)map.Width, (int)map.Height), (int)map.TileHeight);

    var idx = 0f;

    foreach (var layer in map.Layers) {
      if (!layer.Visible) continue;

      if (layer is TileLayer tileLayer) {
        var resultLayer = CreateTilemap(app, tileLayer, map, ref tilemap, ref tileSources, idx);
        if (!resultLayer.IsCollision) {
          tilemap.Layers.Add(resultLayer);
          idx -= 0.1f;
        } else {
          resultLayer.LocalZDepth = -100;
          tilemap.CollisionLayer = resultLayer;
        }

      } else if (layer is ImageLayer imageLayer) {
        CreateImages(app, imageLayer, map, ref tilemap, ref bgSources);
      }
    }

    tilemap.CreateTilemap([.. tileSources]);
    // tilemap.CreateBackgrounds([.. bgSources]);

    return tilemap;
  }

  private static TilemapLayer CreateTilemap(
    Application app,
    DotTiled.TileLayer tileLayer,
    Map map,
    ref Tilemap parent,
    ref List<string> imageSources,
    in float idx
  ) {
    TileInfo[,] tiles = new TileInfo[parent.TilemapSize.X, parent.TilemapSize.Y];

    string imgSrc = "";

    for (int y = 0; y < tileLayer.Height; y++) {
      for (int x = 0; x < tileLayer.Width; x++) {
        var index = x + y * (int)tileLayer.Width;
        var tile = tileLayer.Data.Value.GlobalTileIDs.Value[index];

        var tileInfo = new TileInfo {
          X = x,
          Y = y,
        };

        if (tile == 0) {
          tileInfo.IsNotEmpty = false;
          tileInfo.TextureX = -1;
          tileInfo.TextureY = -1;
          tileInfo.UMin = 0f;
          tileInfo.UMax = 0f;
          tileInfo.VMin = 0f;
          tileInfo.VMax = 0f;
        } else {
          tileInfo.IsNotEmpty = true;
          tileInfo.IsCollision = true;

          Tileset match = null!;
          foreach (var tileset in map.Tilesets) {
            if (tile >= tileset.FirstGID)
              match = tileset;
            else
              break;
          }

          if (match == null) continue;

          // imgSrc = Path.Combine("./Resources", Path.GetFileName(match.Image.Value.Source));
          var srcSearch = FileLookup.FindPathOfAFile(match.Image.Value.Source);
          if (srcSearch != null) {
            imgSrc = srcSearch;
          } else {
            throw new FileNotFoundException("Could not find file for this tileset");
          }

          var localId = tile - match.FirstGID;

          var margin = match.Margin;
          var spacing = match.Spacing;
          var tileWidth = match.TileWidth;
          var tileHeight = match.TileHeight;
          var imageWidth = match.Image.Value.Width;
          var imageHeight = match.Image.Value.Height;

          var tilesPerRow = (imageWidth - 2 * margin + spacing) / (tileWidth + spacing);

          var tileCol = localId % tilesPerRow;
          var tileRow = localId / tilesPerRow;

          var textureX = margin + tileCol * (tileWidth + spacing);
          var textureY = margin + tileRow * (tileHeight + spacing);
          tileInfo.TextureX = (int)textureX;
          tileInfo.TextureY = (int)textureY;

          tileInfo.UMin = (float)textureX / imageWidth;
          tileInfo.UMax = (float)(textureX + tileWidth) / imageWidth;
          tileInfo.VMin = (float)textureY / imageHeight;
          tileInfo.VMax = (float)(textureY + tileHeight) / imageHeight;

          tileInfo.VMin = -tileInfo.VMin;
          tileInfo.VMax = -tileInfo.VMax;

          tiles[x, y] = tileInfo;
        }
      }
    }

    tileLayer.TryGetProperty<BoolProperty>("IsCollision", out var collProperty);
    bool isCollision = false;
    if (collProperty != null && collProperty.Value) {
      isCollision = true;
    }

    return new TilemapLayer(app, parent, tiles, imgSrc, isCollision, idx);
  }

  private static void CreateImages
  (
    Application app,
    DotTiled.ImageLayer imageLayer,
    Map map,
    ref Tilemap parent,
    ref List<BackgroundData> imageSources
  ) {
    var imageSrc = Path.Combine("./Resources", Path.GetFileName(imageLayer.Image.Value.Source));
    var bgData = new BackgroundData {
      ImagePath = imageSrc,
      PositionOffset = new(imageLayer.OffsetX, imageLayer.OffsetY),
      Position = new(imageLayer.X, imageLayer.Y),
      ParallaxHorizontal = imageLayer.ParallaxX,
      ParallaxVertical = imageLayer.ParallaxY,
      Width = (int)imageLayer.Image.Value.Width.Value,
      Height = (int)imageLayer.Image.Value.Height.Value,
    };
    imageSources.Add(bgData);
  }

  /*
  public static Tilemap LoadTilemap_Old(Application app, string tmxPath) {
    var loader = Loader.Default();
    var map = loader.LoadMap(Path.Combine(DwarfPath.AssemblyDirectory, tmxPath));

    if (map.Infinite) throw new NotSupportedException("Loader does not support infinite maps");
    if (map.Layers.Count > 1) throw new NotSupportedException("Loader does not support multiple layers");
    if (map.Layers[0] is not DotTiled.TileLayer tileLayer) throw new ArgumentException("No tile layer found");

    string imageSource = string.Empty;
    TileInfo[,] tiles = new TileInfo[(int)map.Width, (int)map.Height];

    for (int y = 0; y < tileLayer.Height; y++) {
      for (int x = 0; x < tileLayer.Width; x++) {
        var index = x + y * (int)tileLayer.Width;
        var tile = tileLayer.Data.Value.GlobalTileIDs.Value[index];

        var tileInfo = new TileInfo {
          X = x,
          Y = y,
        };

        if (tile == 0) {
          tileInfo.IsNotEmpty = false;
          tileInfo.TextureX = -1;
          tileInfo.TextureY = -1;
          tileInfo.UMin = 0f;
          tileInfo.UMax = 0f;
          tileInfo.VMin = 0f;
          tileInfo.VMax = 0f;
        } else {
          tileInfo.IsNotEmpty = true;

          Tileset match = null!;
          foreach (var tileset in map.Tilesets) {
            if (tile >= tileset.FirstGID)
              match = tileset;
            else
              break;
          }

          if (match == null) continue;

          imageSource = Path.Combine("./Resources", Path.GetFileName(match.Image.Value.Source));
          var localId = tile - match.FirstGID;

          // Tileset properties
          var margin = match.Margin;
          var spacing = match.Spacing;
          var tileWidth = match.TileWidth;
          var tileHeight = match.TileHeight;
          var imageWidth = match.Image.Value.Width;
          var imageHeight = match.Image.Value.Height;

          // Calculate the number of tiles per row on the tileset image.
          var tilesPerRow = (imageWidth - 2 * margin + spacing) / (tileWidth + spacing);

          // Calculate column and row within the tileset.
          var tileCol = localId % tilesPerRow;
          var tileRow = localId / tilesPerRow;

          // Calculate texture position in pixels.
          var textureX = margin + tileCol * (tileWidth + spacing);
          var textureY = margin + tileRow * (tileHeight + spacing);
          tileInfo.TextureX = (int)textureX;
          tileInfo.TextureY = (int)textureY;

          // Compute normalized UV coordinates.
          tileInfo.UMin = (float)textureX / imageWidth;
          tileInfo.UMax = (float)(textureX + tileWidth) / imageWidth;
          tileInfo.VMin = (float)textureY / imageHeight;
          tileInfo.VMax = (float)(textureY + tileHeight) / imageHeight;

          // If your graphics system uses a bottom-left origin for textures,
          // you may need to invert the V coordinates. For example:
          // float tempVMin = tileInfo.VMin;
          // tileInfo.VMin = 1.0f - tileInfo.VMax;
          // tileInfo.VMax = 1.0f - tempVMin;

          tileInfo.VMin = -tileInfo.VMin;
          tileInfo.VMax = -tileInfo.VMax;

          tiles[x, y] = tileInfo;
        }
      }
    }

    var tilemap = new Tilemap(app, new((int)map.Width, (int)map.Height), imageSource, (int)map.TileHeight) {
      Tiles = tiles
    };
    tilemap.CreateTilemap();
    return tilemap;
  }

  */
}