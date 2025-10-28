using Neko.AbstractionLayer;
using Neko.EntityComponentSystem;
using Neko.Math;
using Neko.Vulkan;

namespace Neko.Rendering.Renderer2D.Interfaces;

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
}