using System.Numerics;
using System.Runtime.InteropServices;

namespace Neko.Rendering.Renderer3D;

[StructLayout(LayoutKind.Explicit)]
public struct ModelUniformBufferObject {
  /*
  [FieldOffset(0)] public Vector4 Color;
  [FieldOffset(16)] public Vector3 Ambient;
  [FieldOffset(28)] public Vector3 Diffuse;
  [FieldOffset(40)] public Vector3 Specular;
  [FieldOffset(60)] public float Shininess;
  */

  [FieldOffset(0)] public Vector3 Color;
  [FieldOffset(16)] public Vector3 Ambient;
  [FieldOffset(32)] public Vector3 Diffuse;
  [FieldOffset(48)] public Vector3 Specular;
  [FieldOffset(60)] public float Shininess;
}