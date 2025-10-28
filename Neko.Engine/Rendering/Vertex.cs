using System.Numerics;
using Neko.Math;

namespace Neko.Rendering;

public struct Vertex {
  public Vector3 Position;
  public Vector3 Color;
  public Vector3 Normal;
  public Vector2 Uv;

  public Vector4I JointIndices;
  public Vector4 JointWeights;
}

public struct SimpleVertex {
  public Vector3 Position;
  public Vector3 Color;
  public Vector3 Normal;
  public Vector2 Uv;
}

public struct PositionVertex {
  public Vector3 Position;
}

public struct TexturedVertex {
  public Vector3 Position;
  public Vector3 Color;
  public Vector2 Uv;
}