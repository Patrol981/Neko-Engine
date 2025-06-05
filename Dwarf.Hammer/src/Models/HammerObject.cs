using System.Numerics;
using Dwarf.Hammer.Enums;
using Dwarf.Hammer.Structs;

namespace Dwarf.Hammer.Models;

internal class HammerObject {
  internal Vector2 Position;
  internal Vector2 Velocity;
  internal Vector2 Force;
  internal float Mass = 0.3f;
  internal MotionType MotionType;
  internal MotionQuality MotionQuality;
  internal ObjectType ObjectType;
  internal Mesh Mesh;
  internal AABB AABB = null!;
  internal bool Grounded { get; set; } = false;
  internal bool IsTrigger { get; set; } = false;

  internal Edge[] Edges { get; set; } = [];
  internal AABB[] TilemapAABBs { get; set; } = [];
}