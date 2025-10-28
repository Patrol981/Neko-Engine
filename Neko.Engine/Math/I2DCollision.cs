using System.Numerics;

namespace Neko.Math;

public interface I2DCollision {
  public bool IsUI { get; }
  public Vector2 Size { get; }
  public Bounds2D Bounds { get; }
}
