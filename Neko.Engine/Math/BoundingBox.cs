using System.Numerics;
using Neko.Rendering;
using Neko.Rendering.Renderer3D;

namespace Neko.Math;

public struct BoundingBox {
  public Vector3 Min;
  public Vector3 Max;
  public bool IsValid;

  public BoundingBox() {
    Min = Vector3.Zero;
    Max = Vector3.One;
  }

  public BoundingBox(float min, float max) {
    Min = new Vector3(min, min, min);
    Max = new Vector3(max, max, max);
  }

  public BoundingBox(Vector3 min, Vector3 max) {
    Min = min;
    Max = max;
  }

  public static BoundingBox? GetBoundingBox(in Vertex[]? vertices) {
    if (vertices == null || vertices.Length == 0)
      throw new InvalidOperationException("Mesh has no vertices.");

    Vector3 min = vertices[0].Position;
    Vector3 max = vertices[0].Position;

    foreach (var vertex in vertices) {
      min = Vector3.Min(min, vertex.Position);
      max = Vector3.Max(max, vertex.Position);
    }

    return new BoundingBox(min, max);
  }

  public BoundingBox GetBoundingBox(Matrix4x4 m) {
    var min = new Vector3(m[3, 0], m[3, 1], m[3, 2]);
    var max = min;
    Vector3 v0, v1;

    var right = new Vector3(m[0, 0], m[0, 1], m[0, 2]);
    v0 = right * Min.X;
    v1 = right * max.X;
    min += Vector3.Min(v0, v1);
    max += Vector3.Max(v0, v1);

    var up = new Vector3(m[1, 0], m[1, 1], m[1, 2]);
    v0 = up * Min.Y;
    v1 = up * Max.Y;
    min += Vector3.Min(v0, v1);
    max += Vector3.Max(v0, v1);

    var back = new Vector3(m[2, 0], m[2, 1], m[2, 2]);
    v0 = back * Min.Z;
    v1 = back * Max.Z;
    min += Vector3.Min(v0, v1);
    max += Vector3.Max(v0, v1);

    return new BoundingBox(min, max);
  }
}