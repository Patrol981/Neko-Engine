using Dwarf.EntityComponentSystem;
using Dwarf.Networking.WebApi.Interfaces;
using Dwarf.Rendering.Renderer2D.Components;
using Microsoft.Extensions.Logging;

namespace Dwarf.Networking.WebApi.Services;

public class SpriteService : ISpriteService {
  private readonly ILogger<ISpriteService> _logger;
  private readonly Application _app;

  public SpriteService(ILogger<ISpriteService> logger) {
    _logger = logger;
    _app = Application.Instance;
  }

  public byte[] GetSpriteImage(Guid entityId) {
    var targetEntity = _app.GetEntitiesEnumerable()
      .Where(x => x.Id == entityId)
      .Single();

    var spriteRenderer = targetEntity.GetDrawable2D() as SpriteRenderer;
    var sprite = spriteRenderer?.SpriteSheet[spriteRenderer.CurrentSprite];

    return sprite?.TextureData ?? [];
  }
}