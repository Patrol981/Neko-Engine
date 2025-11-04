using System.Numerics;
using System.Runtime.InteropServices;
using Neko.Math;

namespace Neko.Rendering.Renderer2D.Models;

[StructLayout(LayoutKind.Explicit)]
public struct SpritePushConstant430 {
  [FieldOffset(0)] public Matrix4x4 SpriteMatrix;
  [FieldOffset(64)] public Vector3 SpriteSheetData; // sizeX, sizeY, index
  [FieldOffset(76)] public bool FlipX;
  [FieldOffset(80)] public bool FlipY;
  [FieldOffset(84)] public uint TextureIndex;
  // [FieldOffset(80)] public Vector2I SheetSize;
  // [FieldOffset(88)] public int SpriteIndex;
}

[StructLayout(LayoutKind.Explicit)]
public struct SpritePushConstant140 {
  [FieldOffset(0)] public Matrix4x4 SpriteMatrix;

  /// <summary>
  /// <list type="table">
  /// <item>
  /// <description><b>X</b>: SizeX</description>
  /// </item>
  /// <item>
  /// <description><b>Y</b>: SizeY</description>
  /// </item>
  /// <item>
  /// <description><b>Z</b>: Index</description>
  /// </item>
  /// <item>
  /// <description><b>W</b>: FlipX</description>
  /// </item>
  /// </list>
  /// </summary>
  [FieldOffset(64)] public Vector4 SpriteSheetData; // sizeX, sizeY, index, flipX

  /// <summary>
  /// <list type="table">
  /// <item>
  /// <description><b>X</b>: FlipY</description>
  /// </item>
  /// <item>
  /// <description><b>Y</b>: TextureIndex</description>
  /// </item>
  /// <item>
  /// <description><b>Z</b>: Empty</description>
  /// </item>
  /// <item>
  /// <description><b>W</b>: Empty</description>
  /// </item>
  /// </list>
  /// </summary>
  [FieldOffset(80)] public Vector4 SpriteSheetData2; // flipY, textureIndex
}