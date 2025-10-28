using System.Numerics;
using System.Runtime.InteropServices;

namespace Neko.Rendering.Shadows;

[StructLayout(LayoutKind.Sequential)]
public struct ShadowPushConstant {
  public Matrix4x4 Transform;
  public float Radius;
}