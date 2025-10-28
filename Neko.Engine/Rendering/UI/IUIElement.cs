namespace Neko.Rendering.UI;

public interface IUIElement : IDrawable {
  public void Update();
  public Guid GetTextureIdReference();
  public void DrawText(string text);
}
