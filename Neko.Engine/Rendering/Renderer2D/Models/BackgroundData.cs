using System.Numerics;

namespace Neko.Rendering.Renderer2D.Models;

public struct BackgroundData {
  public string ImagePath { get; set; }
  public Vector2 PositionOffset { get; set; }
  public Vector2 Position { get; set; }
  public float ParallaxHorizontal { get; set; }
  public float ParallaxVertical { get; set; }
  public int Width { get; set; }
  public int Height { get; set; }
}