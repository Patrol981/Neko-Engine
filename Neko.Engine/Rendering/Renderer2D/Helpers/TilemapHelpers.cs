using System.Numerics;
using Neko.EntityComponentSystem;
using Neko.Math;
using Neko.Rendering.Renderer2D.Components;
using Neko.Rendering.Renderer2D.Models;

namespace Neko.Rendering.Renderer2D.Helpers;

public static class TilemapHelpers {
  public static bool IsWithinTilemap(this TileInfo[,] tiles, int x, int y) {
    if (tiles.GetLength(0) < x || tiles.GetLength(1) < y) return false;
    return true;
  }

  public static bool HasUVCoords(this TileInfo tileInfo) {
    return tileInfo.UMin > 0 || tileInfo.UMax > 0 || tileInfo.VMin > 0 || tileInfo.VMax > 0;
  }

  public static List<Edge> ExtractEgdges(this Tilemap tilemap) {
    var collTimemap = tilemap.Layers.Where(x => x.IsCollision).First();
    int w = collTimemap.Tiles.GetLength(0), h = collTimemap.Tiles.GetLength(1);
    var tileSize = tilemap.TileSize;
    var edges = new List<Edge>();
    var scale = tilemap.Entity.GetTransform()!.Scale;

    void AddEdge(int x1, int y1, int x2, int y2) {
      var A = new Vector2(x1 * scale.X, y1 * scale.Y) * tileSize;
      var B = new Vector2(x2 * scale.X, y2 * scale.Y) * tileSize;
      // Edge direction
      var dir = Vector2.Normalize(B - A);
      // Normal = rotate ninety degrees:
      var normal = new Vector2(-dir.Y, dir.X);
      edges.Add(new Edge { A = A, B = B, Normal = normal });
    }

    for (int x = 0; x < w; x++) {
      for (int y = 0; y < h; y++) {
        if (!collTimemap.Tiles[x, y].IsCollision) continue;
        // Check each of the 4 cardinal neighbors:
        if (y + 1 >= h || !collTimemap.Tiles[x, y + 1].IsCollision) AddEdge(x, y + 1, x + 1, y + 1); // top
        if (y - 1 < 0 || !collTimemap.Tiles[x, y - 1].IsCollision) AddEdge(x + 1, y, x, y);     // bottom
        if (x - 1 < 0 || !collTimemap.Tiles[x - 1, y].IsCollision) AddEdge(x, y, x, y + 1);     // left
        if (x + 1 >= w || !collTimemap.Tiles[x + 1, y].IsCollision) AddEdge(x + 1, y + 1, x + 1, y);   // right
      }
    }

    return edges;
  }

  public static List<(Vector2, Vector2)> ExtractAABBs(this Tilemap tilemap) {
    var list = new List<(Vector2, Vector2)>();
    var targetLayer = tilemap.CollisionLayer;
    var tilemapTransform = tilemap.Entity.GetTransform();

    for (int y = 0; y < targetLayer.Tiles.GetLength(1); y++) {
      for (int x = 0; x < targetLayer.Tiles.GetLength(0); x++) {
        var tile = targetLayer.Tiles[x, y];
        if (!tile.IsNotEmpty) continue;

        float pixelsPerUnit = tilemap.TileSize * 10;

        float worldX = tile.X * (tilemap.TileSize / pixelsPerUnit) * tilemapTransform!.Scale.X + tilemapTransform.Position.X;
        float worldY = tile.Y * (tilemap.TileSize / pixelsPerUnit) * tilemapTransform.Scale.Y + tilemapTransform.Position.Y;

        float tileSizeWorld = (float)tilemap.TileSize / pixelsPerUnit * tilemapTransform.Scale.X;
        float tileMinX = worldX + tileSizeWorld / 3;
        float tileMaxX = worldX + tileSizeWorld + (tileSizeWorld / 3);
        float tileMinY = worldY + tileSizeWorld / 3;
        float tileMaxY = worldY + tileSizeWorld + (tileSizeWorld / 3);

        list.Add((new(tileMinX, tileMinY), new(tileMaxX, tileMaxY)));
      }
    }

    return list;
  }

  public static List<(Vector2 center, Vector2 halfExtents)> BuildCollisionRectangles(this Tilemap tilemap) {
    var layer = tilemap.CollisionLayer;
    var tiles = layer.Tiles;

    int width = tiles.GetLength(0);
    int height = tiles.GetLength(1);

    // World size of a single tile in the same space as the mesh vertices.
    // Use the same value you use for tile vertices. In your GenerateMesh this is 0.10f.
    // float tileSize = Sprite.VERTEX_SIZE; // or 0.10f if VERTEX_SIZE is not what you use
    // float tileSize = 0.10f;

    var tilemapTransform = tilemap.Entity.GetTransform();
    float pixelsPerUnit = tilemap.TileSize * 10;
    float tileSize = (float)tilemap.TileSize / pixelsPerUnit * tilemapTransform!.Scale.X;

    var used = new bool[width, height];
    var result = new List<(Vector2 center, Vector2 halfExtents)>();

    for (int y = 0; y < height; y++) {
      for (int x = 0; x < width; x++) {
        var tile = tiles[x, y];

        // Decide what marks a tile as collidable; here I assume IsNotEmpty for your collision layer.
        if (!tile.IsNotEmpty || used[x, y])
          continue;

        // Expand horizontally
        int maxX = x;
        while (maxX + 1 < width &&
               tiles[maxX + 1, y].IsNotEmpty &&
               !used[maxX + 1, y]) {
          maxX++;
        }

        // Expand vertically as long as the full horizontal band is solid and unused
        int maxY = y;
        bool canGrow = true;
        while (canGrow && maxY + 1 < height) {
          for (int ix = x; ix <= maxX; ix++) {
            if (!tiles[ix, maxY + 1].IsNotEmpty || used[ix, maxY + 1]) {
              canGrow = false;
              break;
            }
          }

          if (canGrow)
            maxY++;
        }

        // Mark all tiles in this rectangle as used
        for (int yy = y; yy <= maxY; yy++) {
          for (int xx = x; xx <= maxX; xx++) {
            used[xx, yy] = true;
          }
        }

        // Compute world space rectangle in tilemap local coordinates.
        int tilesWide = maxX - x + 1;
        int tilesHigh = maxY - y + 1;

        float rectWidth = tilesWide * tileSize;
        float rectHeight = tilesHigh * tileSize;

        float centerX = (x + tilesWide * 0.5f) * tileSize;
        float centerY = (y + tilesHigh * 0.5f) * tileSize;

        var center = new Vector2(centerX, centerY);
        var halfExtents = new Vector2(rectWidth * 0.5f, rectHeight * 0.5f);

        result.Add((center, halfExtents));
      }
    }

    return result;
  }
}