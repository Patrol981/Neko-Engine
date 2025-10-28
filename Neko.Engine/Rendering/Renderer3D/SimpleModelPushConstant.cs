using System.Numerics;
using System.Runtime.InteropServices;

namespace Neko.Rendering.Renderer3D;

[StructLayout(LayoutKind.Explicit)]
public struct SimpleModelPushConstant {
  [FieldOffset(0)] public Matrix4x4 ModelMatrix;
  [FieldOffset(64)] public Matrix4x4 NormalMatrix;

  public static SimpleModelPushConstant New() {
    return new SimpleModelPushConstant {
      ModelMatrix = Matrix4x4.Identity,
      NormalMatrix = Matrix4x4.Identity
    };
  }
}