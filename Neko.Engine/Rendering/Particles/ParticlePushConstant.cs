using System.Numerics;

namespace Neko.Rendering.Particles;

public struct ParticlePushConstant {
  public Vector4 Position;
  public Vector4 Color;
  public float Radius;
  public float Rotation;
}