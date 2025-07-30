using System.Numerics;
using Dwarf.Hammer.Structs;

namespace Dwarf.Hammer.Models;

internal class AABB {
  internal Vector2 Min;
  internal Vector2 Max;

  internal float Width => Max.X - Min.X;
  internal float Height => Max.Y - Min.Y;

  internal static AABB[] CreateAABBListFromTilemap(List<(Vector2, Vector2)> minMaxes) {
    var list = new List<AABB>();

    foreach (var minMax in minMaxes) {
      list.Add(new AABB() {
        Min = minMax.Item1,
        Max = minMax.Item2,
      });
    }

    return [.. list];
  }

  internal static AABB ComputeAABB(HammerObject hammerObject) {
    var verts = hammerObject.Mesh?.Vertices ?? [];
    float minX = float.MaxValue, minY = float.MaxValue;
    float maxX = float.MinValue, maxY = float.MinValue;

    foreach (var v in verts) {
      minX = MathF.Min(minX, v.X);
      minY = MathF.Min(minY, v.Y);
      maxX = MathF.Max(maxX, v.X);
      maxY = MathF.Max(maxY, v.Y);
    }

    return new AABB {
      Min = new Vector2(minX, minY),
      Max = new Vector2(maxX, maxY)
    };
  }

  internal static List<AABB> BuildAABBsFromMesh(Mesh mesh, Vector2 meshOrigin) {
    var aabbs = new List<AABB>();
    const int indicesPerTile = 6;           // 2 triangles×3 indices
    int tileCount = mesh.Indices.Length / indicesPerTile;

    for (int tile = 0; tile < tileCount; tile++) {
      // track bounds in tile‑local mesh coords
      float minX = float.MaxValue, minY = float.MaxValue;
      float maxX = float.MinValue, maxY = float.MinValue;

      // collect the unique 4 vertices for this quad
      var vidSet = new HashSet<uint>();
      int baseIdx = tile * indicesPerTile;
      for (int k = 0; k < indicesPerTile; k++)
        vidSet.Add(mesh.Indices[baseIdx + k]);

      foreach (uint vid in vidSet) {
        var v = mesh.Vertices[vid];
        minX = MathF.Min(minX, v.X);
        minY = MathF.Min(minY, v.Y);
        maxX = MathF.Max(maxX, v.X);
        maxY = MathF.Max(maxY, v.Y);
      }

      // convert to world space by adding the mesh’s origin
      var worldMin = new Vector2(minX, minY);
      var worldMax = new Vector2(maxX, maxY);
      aabbs.Add(new AABB { Min = worldMin, Max = worldMax });
    }

    return aabbs;
  }

  internal bool CheckCollision(Vector2 aPos, Vector2 bPos, AABB bAABB) {
    bool collX = aPos.X + Width >= bPos.X && bPos.X + bAABB.Width >= aPos.X;
    bool collY = aPos.Y + Height >= bPos.Y && bPos.Y + bAABB.Height >= aPos.Y;

    return collX && collY;
  }

  internal static bool CheckCollisionMTV(HammerObject? a, HammerObject? b, out Vector2 mtv) {
    if (a == null || b == null) {
      mtv = Vector2.Zero;
      return false;
    }

    float aMinX = a.Position.X;
    float aMaxX = aMinX + a.AABB?.Width ?? 0;
    float aMinY = a.Position.Y;
    float aMaxY = aMinY + a.AABB?.Height ?? 0;

    float bMinX = b.Position.X;
    float bMaxX = bMinX + b.AABB?.Width ?? 0;
    float bMinY = b.Position.Y;
    float bMaxY = bMinY + b.AABB?.Height ?? 0;

    bool overlapX = aMaxX > bMinX && bMaxX > aMinX;
    bool overlapY = aMaxY > bMinY && bMaxY > aMinY;

    if (overlapX && overlapY) {
      float overlapXAmount = Math.Min(aMaxX, bMaxX) - Math.Max(aMinX, bMinX);
      float overlapYAmount = Math.Min(aMaxY, bMaxY) - Math.Max(aMinY, bMinY);

      if (overlapXAmount < overlapYAmount) {
        float direction = (a.Position.X < b.Position.X) ? -1f : 1f;
        mtv = new Vector2(overlapXAmount * direction, 0);
      } else {
        float direction = (a.Position.Y < b.Position.Y) ? -1f : 1f;
        mtv = new Vector2(0, overlapYAmount * direction);
      }

      return true;
    }

    mtv = Vector2.Zero;
    return false;
  }

  internal static bool CheckCollisionWithTilemap(
    HammerObject tilemap,
    AABB tilemapAABB,
    float minX,
    float maxX,
    float minY,
    float maxY
  ) {
    bool overlapX = tilemapAABB.Max.X > minX && tilemapAABB.Min.X < maxX;
    bool overlapY = tilemapAABB.Max.Y > minY && tilemapAABB.Min.Y < maxY;

    return overlapX && overlapY;
  }

  internal static bool CheckCollisionWithTilemapMTV(
    AABB? spriteAABB,
    Vector2 spritePos,
    AABB? tileAABB,
    Vector2 tilePos,
    out Vector2 mtv) {
    mtv = Vector2.Zero;

    if (spriteAABB == null || tileAABB == null)
      return false;

    float aMinX = spritePos.X;
    float aMaxX = aMinX + spriteAABB.Width;
    float aMinY = spritePos.Y;
    float aMaxY = aMinY + spriteAABB.Height;

    float bMinX = tileAABB.Min.X;
    float bMaxX = tileAABB.Max.X;
    float bMinY = tileAABB.Min.Y;
    float bMaxY = tileAABB.Max.Y;

    bool overlapX = aMaxX > bMinX && bMaxX > aMinX;
    bool overlapY = aMaxY > bMinY && bMaxY > aMinY;

    if (!overlapX || !overlapY)
      return false;

    float overlapXAmount = MathF.Min(aMaxX, bMaxX) - MathF.Max(aMinX, bMinX);
    float overlapYAmount = MathF.Min(aMaxY, bMaxY) - MathF.Max(aMinY, bMinY);

    if (overlapXAmount < overlapYAmount) {
      bool fromLeft = (aMinX + spriteAABB.Width / 2f) < (bMinX + tileAABB.Width / 2f);
      mtv = new Vector2(fromLeft ? -overlapXAmount : overlapXAmount, 0);
    } else {
      bool fromTop = (aMinY + spriteAABB.Height / 2f) < (bMinY + tileAABB.Height / 2f);
      mtv = new Vector2(0, fromTop ? -overlapYAmount : overlapYAmount);
    }

    return true;
  }

  internal static bool CheckCollisionWithTilemapMTV_(
    AABB? spriteAABB,
    Vector2 spritePos,
    AABB? tileAABB,
    Vector2 tilePos,
    out Vector2 mtv) {
    float aMinX = spritePos.X;
    float aMaxX = aMinX + spriteAABB?.Width ?? 0;
    float aMinY = spritePos.Y;
    float aMaxY = aMinY + spriteAABB?.Height ?? 0;

    // float bMinX = tilePos.X;
    // float bMaxX = bMinX + tileAABB.Width;
    // float bMinY = tilePos.Y;
    // float bMaxY = bMinY + tileAABB.Height;

    float bMinX = tileAABB?.Min.X ?? 0;
    float bMaxX = tileAABB?.Max.X ?? 0;
    float bMinY = tileAABB?.Min.Y ?? 0;
    float bMaxY = tileAABB?.Max.Y ?? 0;

    bool overlapX = aMaxX > bMinX && bMaxX > aMinX;
    bool overlapY = aMaxY > bMinY && bMaxY > aMinY;

    if (overlapX && overlapY) {
      float overlapXAmount = Math.Min(aMaxX, bMaxX) - Math.Max(aMinX, bMinX);
      float overlapYAmount = Math.Min(aMaxY, bMaxY) - Math.Max(aMinY, bMinY);

      if (overlapXAmount < overlapYAmount) {
        float direction = (spritePos.X < tilePos.X) ? -1f : 1f;
        mtv = new Vector2(overlapXAmount * direction, 0);
      } else {
        float direction = (spritePos.Y < tilePos.Y) ? -1f : 1f;
        mtv = new Vector2(0, overlapYAmount * direction);
      }

      return true;
    }

    mtv = Vector2.Zero;
    return false;
  }

  internal static bool CollideAndResolve(HammerObject hammerObject, ReadOnlySpan<Edge> candidates) {
    Vector2[] worldVerts = [.. hammerObject.Mesh?.Vertices.Select(v => new Vector2(v.X, v.Y)) ?? []];

    float minPen = float.MaxValue;
    Vector2 minAxis = Vector2.Zero;


    foreach (var e in candidates) {
      // Project your polygon onto the edge normal:
      float minP = +float.MaxValue, maxP = -float.MaxValue;
      foreach (var wv in worldVerts) {
        float proj = Vector2.Dot(wv, e.Normal);
        minP = MathF.Min(minP, proj);
        maxP = MathF.Max(maxP, proj);
      }
      // Project one endpoint of the edge onto the normal:
      float edgeProj = Vector2.Dot(e.A, e.Normal);

      // Compute penetration: how much dyn overlaps *past* the wall
      float pen = maxP - edgeProj;
      // If entirely on the non‐penetrating side, no collision on this axis:
      if (pen < 0) return false;

      if (pen < minPen) {
        minPen = pen;
        minAxis = e.Normal;
      }
    }

    Console.WriteLine("BRUA");

    hammerObject.Position += minAxis * minPen;
    float vn = Vector2.Dot(hammerObject.Velocity, minAxis);
    if (vn < 0) {
      // simple inelastic: remove component into wall
      hammerObject.Velocity -= minAxis * vn;
    }
    return true;
  }
}

