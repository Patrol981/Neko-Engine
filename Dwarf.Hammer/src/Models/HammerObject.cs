using System.Numerics;
using Dwarf.Hammer.Enums;
using Dwarf.Hammer.Structs;

namespace Dwarf.Hammer.Models;

internal class HammerObject {
  internal Vector2 Position = Vector2.Zero;
  internal Vector2 Velocity = Vector2.Zero;
  internal Vector2 Force = Vector2.Zero;
  internal float Mass = 0.3f;
  internal MotionType MotionType = MotionType.Static;
  internal MotionQuality MotionQuality = MotionQuality.LinearCast;
  internal ObjectType ObjectType = ObjectType.Sprite;
  internal Mesh? Mesh;
  internal AABB? AABB = null!;
  internal bool Grounded { get; set; } = false;
  internal bool IsTrigger { get; set; } = false;

  internal Edge[] Edges { get; set; } = [];
  internal AABB[] TilemapAABBs { get; set; } = [];
}