
using System.Numerics;
using System.Runtime.InteropServices;

namespace Neko.Physics;

[StructLayout(LayoutKind.Explicit)]
public struct ColliderMeshPushConstant {
  [FieldOffset(0)] public Matrix4x4 ModelMatrix;
}