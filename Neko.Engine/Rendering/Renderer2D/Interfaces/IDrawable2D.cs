using Neko.AbstractionLayer;
using Neko.EntityComponentSystem;
using Neko.Math;

namespace Neko.Rendering.Renderer2D.Interfaces;

public enum Drawable2DType {
  Sprite,
  Tilemap,
  TilemapLayer
}

public interface IDrawable2D : IDrawable, ICloneable {
  Entity Entity { get; }
  bool Active { get; }
  ITexture Texture { get; }
  ITexture[] SpriteSheet { get; }
  Vector2I SpriteSheetSize { get; }
  int SpriteIndex { get; }
  int SpriteCount { get; }
  bool FlipX { get; set; }
  bool FlipY { get; set; }
  float LocalZDepth { get; }
  Mesh CollisionMesh { get; }
  Mesh Mesh { get; }
  public IDrawable2D[] Children { get; }
  public Drawable2DType DrawableType { get; }

  ShaderInfo CustomShader { get; }
  void SetCustomShader(ShaderInfo shaderInfo);
  void SetShaderTextureInfo(Guid textureId);
}