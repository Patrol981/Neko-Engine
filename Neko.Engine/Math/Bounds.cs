using System.Numerics;

namespace Neko.Math;

public struct Bounds2D {
  public Vector2 Min;
  public Vector2 Max;

  public Bounds2D() { }
  public Bounds2D(Vector2 min, Vector2 max) {
    Min = min;
    Max = max;
  }

  public static Bounds2D Zero {
    get {
      return new Bounds2D(Vector2.Zero, Vector2.Zero);
    }
  }
}
