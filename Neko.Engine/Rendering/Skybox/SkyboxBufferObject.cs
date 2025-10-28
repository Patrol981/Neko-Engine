using System.Numerics;
using System.Runtime.InteropServices;

namespace Neko;

[StructLayout(LayoutKind.Explicit)]
public struct SkyboxBufferObject {
  [FieldOffset(0)] public Matrix4x4 SkyboxMatrix;
  [FieldOffset(64)] public Vector3 SkyboxColor;
}