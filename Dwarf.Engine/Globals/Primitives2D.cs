using System.Numerics;
using Dwarf.Rendering;

namespace Dwarf.Globals;

public static class Primitives2D {
  public static Mesh CreateQuad2D(Vector2 min, Vector2 max) {
    var app = Application.Instance;
    var mesh = new Mesh(app.Allocator, app.Device) {
      Vertices = new Vertex[4],
      Indices = [
        0, 1, 2,
         0, 2, 3
      ]
    };

    var bl = new Vector3(min.X, min.Y, 0.0f); // bottom‑left
    var br = new Vector3(max.X, min.Y, 0.0f); // bottom‑right
    var tr = new Vector3(max.X, max.Y, 0.0f); // top‑right
    var tl = new Vector3(min.X, max.Y, 0.0f); // top‑left

    mesh.Vertices[0] = new Vertex {
      Position = bl,
      Uv = new Vector2(0.0f, 1.0f),
      Color = new Vector3(1, 1, 1),
      Normal = new Vector3(0, 0, 1)
    };
    mesh.Vertices[1] = new Vertex {
      Position = br,
      Uv = new Vector2(1.0f, 1.0f),
      Color = new Vector3(1, 1, 1),
      Normal = new Vector3(0, 0, 1)
    };
    mesh.Vertices[2] = new Vertex {
      Position = tr,
      Uv = new Vector2(1.0f, 0.0f),
      Color = new Vector3(1, 1, 1),
      Normal = new Vector3(0, 0, 1)
    };
    mesh.Vertices[3] = new Vertex {
      Position = tl,
      Uv = new Vector2(0.0f, 0.0f),
      Color = new Vector3(1, 1, 1),
      Normal = new Vector3(0, 0, 1)
    };

    return mesh;
  }
}