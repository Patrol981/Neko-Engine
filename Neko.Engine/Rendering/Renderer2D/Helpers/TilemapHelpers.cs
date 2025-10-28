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
}