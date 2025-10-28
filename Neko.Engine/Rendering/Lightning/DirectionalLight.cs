using System.Numerics;
using System.Runtime.InteropServices;

namespace Neko.Rendering.Lightning;

[StructLayout(LayoutKind.Explicit)]
public struct DirectionalLight {
  [FieldOffset(0)] public Vector3 LightPosition;
  [FieldOffset(12)] public float LightIntensity;
  [FieldOffset(16)] public Vector4 LightColor;
  [FieldOffset(32)] public Vector4 AmbientColor;

  public static DirectionalLight New() {
    return new DirectionalLight {
      LightPosition = new(0, -5, 0),
      LightIntensity = 1,
      LightColor = new(1, 1, 1, 1),
      AmbientColor = new(1, 1, 1, 1)
    };
  }
}
