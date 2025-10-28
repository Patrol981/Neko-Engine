using Neko.Math;

namespace Neko.Physics;

public interface ICollision {
  public AABB[] AABBArray { get; }
  public AABB AABB { get; }
}
