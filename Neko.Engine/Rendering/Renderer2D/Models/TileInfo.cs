namespace Neko.Rendering.Renderer2D.Models;

public struct TileInfo {
  public int X;
  public int Y;
  public int TextureX;
  public int TextureY;
  public float UMin;
  public float UMax;
  public float VMin;
  public float VMax;
  public bool IsNotEmpty;
  public bool IsCollision;
}