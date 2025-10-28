using System.Numerics;
using System.Runtime.InteropServices;

namespace Neko.Rendering.Renderer3D.Animations;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct JointUBO {
  [FieldOffset(0)] public int JointsCount;
  [FieldOffset(16)] public Matrix4x4* Joints;
}