using System.Numerics;
using System.Runtime.InteropServices;
using Dwarf.Math;

namespace Dwarf.Rendering.Renderer2D.Models;

[StructLayout(LayoutKind.Explicit)]
public struct SpritePushConstant {
  [FieldOffset(0)] public Matrix4x4 SpriteMatrix;
  [FieldOffset(64)] public Vector3 SpriteSheetData; // sizeX, sizeY, index
  [FieldOffset(76)] public bool FlipX;
  [FieldOffset(80)] public bool FlipY;
  [FieldOffset(84)] public uint TextureIndex;
  // [FieldOffset(80)] public Vector2I SheetSize;
  // [FieldOffset(88)] public int SpriteIndex;
}