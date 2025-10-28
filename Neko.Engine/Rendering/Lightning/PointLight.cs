using System.Numerics;
using System.Runtime.InteropServices;

namespace Neko.Rendering.Lightning;

[StructLayout(LayoutKind.Explicit)]
public struct PointLight {
  [FieldOffset(0)] public Vector4 LightColor;
  [FieldOffset(16)] public Vector4 LightPosition;

  public static PointLight New() {
    return new PointLight {
      LightColor = new(1, 1, 1, 1),
      LightPosition = new(0, 0, 0, 0)
    };
  }
}
