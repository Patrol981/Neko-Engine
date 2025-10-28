using System.Numerics;
using System.Runtime.InteropServices;

namespace Neko.Rendering.Guizmos;

[StructLayout(LayoutKind.Explicit)]
public struct GuizmoBufferObject {
  [FieldOffset(0)] public Matrix4x4 ModelMatrix;
  [FieldOffset(64)] public int GuizmoType;
  [FieldOffset(68)] public float ColorX;
  [FieldOffset(72)] public float ColorY;
  [FieldOffset(76)] public float ColorZ;
}

