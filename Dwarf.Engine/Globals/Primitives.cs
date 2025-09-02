using System.Numerics;
using Dwarf.Rendering;
using Dwarf.Rendering.Renderer3D;

namespace Dwarf;

public enum PrimitiveType {
  Cylinder,
  Box,
  Capsule,
  Convex,
  Sphere,
  Torus,
  Plane,
  None
}

public static class Primitives {
  static Vector3 Color = new(0f, 1f, 0f);

  public static Mesh CreatePrimitive(PrimitiveType primitiveType) {
    var mesh = new Mesh(Application.Instance.Allocator, Application.Instance.Device);

    switch (primitiveType) {
      case PrimitiveType.Cylinder:
        mesh = CreateCylinderPrimitive(2, 2, 20);
        break;
      case PrimitiveType.Box:
        mesh = CreateBoxPrimitive(1);
        break;
      case PrimitiveType.Sphere:
        mesh = CreateSpherePrimitve(128, 128);
        break;
      default:
      case PrimitiveType.Plane:
        // mesh = CreatePlanePrimitive(16, 64);
        mesh = CreatePlanePrimitive(
          new(0, 0, 0),
          new(50, 50),
          new(15, 15),
          new(15, 15)
        );
        break;
    }

    return mesh;
  }

  public static Mesh CreatePlanePrimitive(
    Vector3 bottomLeft,
    Vector2 numVertices,
    Vector2 worldSize,
    Vector2 textureRepetition
  ) {
    var numVerts = (int)(numVertices.X * numVertices.Y);
    var numFaces = (int)((numVertices.X - 1) * (numVertices.Y - 1));
    var numIndices = numFaces * 6;

    var xStep = worldSize.X / (numVertices.X - 1);
    var yStep = worldSize.Y / (numVertices.Y - 1);
    var zStep = worldSize.Y / (numVertices.Y - 1);

    var uStep = textureRepetition.X / (numVertices.X - 1);
    var vStep = textureRepetition.Y / (numVertices.Y - 1);

    var vertices = new Vertex[numVerts];
    var indices = new uint[numIndices];

    for (int y = 0; y < numVertices.Y; y++) {
      for (int x = 0; x < numVertices.X; x++) {
        vertices[x + (int)(y * numVertices.X)] = new() {
          Position = new(
            bottomLeft.X + (xStep * x),
            bottomLeft.Y,
            bottomLeft.Z + (zStep * y)
          ),
          Color = Vector3.One,
          Normal = -Vector3.UnitY
        };

        vertices[(int)(y * numVertices.X + x)].Uv = new(uStep * x, vStep * y);
      }
    }

    int offset = 0;
    for (int i = 0; i < numIndices; i++) {
      var cornerIndex = i / 6 + offset;

      if ((cornerIndex + 1) % numVertices.X == 0) {
        offset++;
        cornerIndex++;
      }

      indices[i] = (uint)cornerIndex + (uint)numVertices.X;
      i++;
      indices[i] = (uint)cornerIndex;
      i++;
      indices[i] = (uint)cornerIndex + (uint)numVertices.X + 1;
      i++;

      indices[i] = (uint)cornerIndex;
      i++;
      indices[i] = (uint)cornerIndex + 1;
      i++;
      indices[i] = (uint)cornerIndex + (uint)numVertices.X + 1;
    }

    return new Mesh(Application.Instance.Allocator, Application.Instance.Device) {
      Vertices = vertices,
      Indices = indices,
      Matrix = Matrix4x4.Identity
    };
  }

  public static Mesh CreatePlanePrimitive(int numOfDivs, float width) {
    List<Vertex> vertices = [];

    var triangleSide = width / numOfDivs;
    for (int row = 0; row < numOfDivs + 1; row++) {
      for (int col = 0; col < numOfDivs + 1; col++) {
        var currentVec = new Vector3(
          col * triangleSide,
          0.0f,
          row * -triangleSide
        );
        vertices.Add(new() {
          Position = currentVec,
          Color = Vector3.One,
          Normal = Vector3.Zero,
        });
      }
    }
    return new Mesh(Application.Instance.Allocator, Application.Instance.Device) {
      Vertices = [.. vertices],
      Matrix = Matrix4x4.Identity
    };
  }

  public static Mesh? CreateConvex(Node[] inputMesh) {
    throw new NotImplementedException();
  }

  public static Mesh CreateConvex(Application app, Node[] nodes, bool flip = false) {
    var vertices = new List<Vertex>();
    var indices = new List<uint>();

    uint vertexOffset = 0;

    foreach (var n in nodes) {
      var mesh = app.Meshes[n.MeshGuid];
      for (int vertexIndex = 0; vertexIndex < mesh.Vertices.Length; vertexIndex++) {
        var vertex = mesh.Vertices[vertexIndex];
        Vector3 updatePos = flip ? new(vertex.Position.X, -vertex.Position.Y, vertex.Position.Z) : vertex.Position;

        vertex.Position = updatePos;
        vertices.Add(vertex);
      }

      foreach (var index in mesh.Indices) {
        indices.Add(index + vertexOffset);
      }

      vertexOffset += (uint)mesh.Vertices.Length;
    }

    return new Mesh(Application.Instance.Allocator, Application.Instance.Device) {
      Vertices = [.. vertices],
      Indices = [.. indices],
      Matrix = Matrix4x4.Identity
    };
  }

  public static Mesh CreateConvex(Mesh mesh, bool flip = false) {
    var vertices = new List<Vertex>();

    for (int vertexIndex = 0; vertexIndex < mesh.Vertices.Length; vertexIndex++) {
      var vertex = mesh.Vertices[vertexIndex];
      Vector3 updatePos = flip ? new(vertex.Position.X, -vertex.Position.Y, vertex.Position.Z) : vertex.Position;

      vertex.Position = updatePos;
      vertex.Color = Color;
      vertices.Add(vertex);
    }

    return new Mesh(Application.Instance.Allocator, Application.Instance.Device) {
      Vertices = [.. vertices],
      Indices = [.. mesh.Indices],
      Matrix = Matrix4x4.Identity
    };
  }

  public static Mesh CreatePlanePrimitive(float scale) {
    Vector3 normal = new(0, 1, 0); // Upward-facing normal (Y-axis)
    Vector2[] uvs = [
        new Vector2(0, 0),
        new Vector2(1, 0),
        new Vector2(1, 1),
        new Vector2(0, 1)
    ];

    Vertex[] vertices = [
        new Vertex { Position = new Vector3(-scale, 0, -scale), Color = Color, Normal = normal, Uv = uvs[0] },
        new Vertex { Position = new Vector3(scale, 0, -scale), Color = Color, Normal = normal, Uv = uvs[1] },
        new Vertex { Position = new Vector3(scale, 0, scale), Color = Color, Normal = normal, Uv = uvs[2] },
        new Vertex { Position = new Vector3(-scale, 0, scale), Color = Color, Normal = normal, Uv = uvs[3] }
    ];

    uint[] indices = [
        0, 1, 2, // First triangle
        2, 3, 0  // Second triangle
    ];

    return new Mesh(Application.Instance.Allocator, Application.Instance.Device) {
      Vertices = vertices,
      Indices = indices,
      Matrix = Matrix4x4.Identity
    };
  }

  public static Mesh CreateBoxPrimitive(float scale) {
    Vector3[] normals = [
      new(-1, -1, -1),
      new(1, -1, -1),
      new(1, 1, -1),
      new(-1, 1, -1),

      new(-1, -1, 1),
      new(1, -1, 1),
      new(1, 1, 1),
      new(-1, 1, 1),
    ];

    Vertex[] vertices = [
      new Vertex { Position = new Vector3(-scale, -scale, -scale), Color = Color, Normal = normals[0], Uv = Vector2.Zero },
      new Vertex { Position = new Vector3(scale, -scale, -scale), Color = Color, Normal = normals[1], Uv = Vector2.Zero },
      new Vertex { Position = new Vector3(scale, scale, -scale), Color = Color, Normal = normals[2], Uv = Vector2.Zero },
      new Vertex { Position = new Vector3(-scale, scale, -scale), Color = Color, Normal = normals[3], Uv = Vector2.Zero },
      new Vertex { Position = new Vector3(-scale, -scale, scale), Color = Color, Normal = normals[4], Uv = Vector2.Zero },
      new Vertex { Position = new Vector3(scale, -scale, scale), Color = Color, Normal = normals[5], Uv = Vector2.Zero },
      new Vertex { Position = new Vector3(scale, scale, scale), Color = Color, Normal = normals[6], Uv = Vector2.Zero },
      new Vertex { Position = new Vector3(-scale, scale, scale), Color = Color, Normal = normals[7], Uv = Vector2.Zero }
    ];

    uint[] indices = [
      // Front face
      0,
      3,
      2,
      2,
      1,
      0,

      // Back face
      4,
      5,
      6,
      6,
      7,
      4,

      // Left face
      0,
      4,
      7,
      7,
      3,
      0,

      // Right face
      1,
      2,
      6,
      6,
      5,
      1,

      // Top face
      3,
      7,
      6,
      6,
      2,
      3,

      // Bottom face
      0,
      1,
      5,
      5,
      4,
      0
    ];

    return new Mesh(Application.Instance.Allocator, Application.Instance.Device) {
      Vertices = vertices,
      Indices = indices,
      Matrix = Matrix4x4.Identity
    };
  }

  public static Mesh CreateCylinderPrimitive(float radius = 0.5f, float height = 1.0f, int segments = 20) {
    float cylinderStep = (MathF.PI * 2) / segments;
    var vertices = new List<Vertex>();
    var indices = new List<uint>();

    for (int i = 0; i < segments; ++i) {
      float theta = cylinderStep * i;
      float x = radius * (float)MathF.Cos(theta);
      float z = radius * (float)MathF.Sin(theta);
      // var pos = new Vector3(x, 0.0f, z);
      var y = 0.0f;
      var pos = new Vector3(x, y, z);
      var normal = Vector3.Normalize(new Vector3(x, y, z));
      var vertex = new Vertex();

      vertex.Position = pos;
      vertex.Normal = normal;
      vertex.Color = Color;
      vertices.Add(vertex);

      pos.Y = -height;
      normal = -normal;
      vertex.Position = pos;
      vertex.Normal = normal;
      vertices.Add(vertex);
    }

    var topCenter = new Vector3(0.0f, -height, 0.0f);
    var bottomCenter = new Vector3(0.0f, 0.0f, 0.0f);

    var vertexTop = new Vertex();
    vertexTop.Position = topCenter;
    vertexTop.Normal = new(0.0f, 1.0f, 0.0f);
    vertices.Add(vertexTop);

    vertexTop.Position = bottomCenter;
    vertexTop.Normal = new(0.0f, -1.0f, 0.0f);
    vertices.Add(vertexTop);

    for (int i = 0; i < segments; ++i) {
      uint top1 = (uint)(2 * i);
      uint top2 = ((uint)((i + 1) % segments) * 2);
      uint bottom1 = top1 + 1;
      uint bottom2 = top2 + 1;

      indices.Add(top1);
      indices.Add(top2);
      indices.Add(bottom1);


      indices.Add(bottom2);
      indices.Add(bottom1);
      indices.Add(top2);

      indices.Add((uint)vertices.Count() - 1);
      indices.Add(top2);
      indices.Add(top1);

      indices.Add((uint)vertices.Count() - 2);
      indices.Add(bottom1);
      indices.Add(bottom2);
    }

    return new Mesh(Application.Instance.Allocator, Application.Instance.Device) {
      Vertices = [.. vertices],
      Indices = [.. indices],
      Matrix = Matrix4x4.Identity
    };
  }

  public static Mesh CreateSpherePrimitve(int slices, int stacks) {
    // List<Vertex> vertices = new();
    var vertices = new Vertex[slices * stacks];
    int index = 0;

    // top vertex
    /*
    vertices.Add(new() {
      Position = new(0, 1, 0),
      Normal = new(1, 1, 1)
    });
    */

    // generate vertices per stack / slice
    for (int i = 0; i < slices; i++) {
      for (int j = 0; j < stacks - 1; j++) {
        var x = MathF.Sin(MathF.PI * i / slices) * MathF.Cos(2 * MathF.PI * j / stacks);
        var y = MathF.Sin(MathF.PI * i / slices) * MathF.Sin(2 * MathF.PI * j / stacks);
        var z = MathF.Cos(MathF.PI * i / slices);
        vertices[index++] = (new() {
          Position = new(x, y, z),
          Normal = new(0, -1, 0),
          Color = new(1, 1, 1)
        });
      }
    }

    /*
    for (int i = 0; i < slices - 1; i++) {
      var phi = MathF.PI * (i + 1) / stacks;
      for (int j = 0; j < slices; j++) {
        var theta = 2.0f * MathF.PI * j / slices;
        var x = MathF.Sin(phi) * MathF.Cos(theta);
        var y = MathF.Cos(phi);
        var z = MathF.Sin(phi) * MathF.Sin(theta);
        vertices.Add(new() {
          Position = new(x, y, z),
          Normal = new(1, 1, 1)
        });
      }
    }
    */

    // bottom vertex
    /*
    vertices.Add(new() {
      Position = new(0, -1, 0),
      Normal = new(1, 1, 1)
    });
    */

    /*
    // top and bottom triangles
    for (int i = 0; i < slices; ++i) {
      var i0 = i + 1;
      var i1 = (i + 1) % slices + 1;
      vertices.AddRange([
        new() {
          Position = new(0, 1, 0),
          Normal = new(1,1,1)
        },
        new() {
          Position = new(i1, i1, i1),
          Normal = new(1,1,1)
        },
        new() {
          Position = new(i0, i0, i0),
          Normal = new(1,1,1)
        },
      ]);

      i0 = i + slices * (stacks - 2) + 1;
      i1 = (i + 1) % slices + slices * (stacks - 2) + 1;
      vertices.AddRange([
        new() {
          Position = new(0, -1, 0),
          Normal = new(1,1,1)
        },
        new() {
          Position = new(i0, i0, i0),
          Normal = new(1,1,1)
        },
        new() {
          Position = new(i1, i1, i1),
          Normal = new(1,1,1)
        },
      ]);
    }

    // quads per stack
    for (int j = 0; j < stacks - 2; j++) {
      var j0 = j * slices + 1;
      var j1 = (j + 1) * slices + 1;
      for (int i = 0; i < slices; i++) {
        var i0 = j0 + i;
        var i1 = j0 + (i + 1) % slices;
        var i2 = j1 + (i + 1) % slices;
        var i3 = j1 + i;
        vertices.AddRange([
          new() {
            Position = new(i0, i0, i0),
            Normal = new(1,1,1)
          },
          new() {
            Position = new(i1, i1, i1),
            Normal = new(1,1,1)
          },
          new() {
            Position = new(i2, i2, i2),
            Normal = new(1,1,1)
          },
          new() {
            Position = new(i3, i3, i3),
            Normal = new(1,1,1)
          }
        ]);
      }
    }
    */

    return new Mesh(Application.Instance.Allocator, Application.Instance.Device) {
      Vertices = vertices,
      Matrix = Matrix4x4.Identity
    };
  }

  private static Vector3[] CalculateNormals(Vertex[] vertices, uint[] indices) {
    var normals = new List<Vector3>();

    for (int i = 0; i < indices.Length; i += 3) {
      var i0 = indices[i];
      var i1 = indices[i + 1];
      var i2 = indices[i + 2];

      var v0 = vertices[i0].Position;
      var v1 = vertices[i1].Position;
      var v2 = vertices[i2].Position;

      var normal = CalculateTriangleNormal([v0, v1, v2]);
      normals.Add(normal);

      vertices[i0].Normal = normal;
      vertices[i1].Normal = normal;
      vertices[i2].Normal = normal;
    }

    return [.. normals];
  }

  private static Vector3 CalculateTriangleNormal(Vector3[] triangle) {
    var u = triangle[1] - triangle[0];
    var v = triangle[2] - triangle[0];

    var normal = new Vector3 {
      X = (u.Y * v.Z) - (u.Z * v.Y),
      Y = (u.Z * v.Y) - (u.X * v.Z),
      Z = (u.X * v.Y) - (u.Y * v.X)
    };

    return normal;
  }
}
