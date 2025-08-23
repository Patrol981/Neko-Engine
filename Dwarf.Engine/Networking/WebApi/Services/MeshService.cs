using Dwarf.Networking.WebApi.Interfaces;
using Dwarf.Networking.WebApi.Mappers;
using Dwarf.Networking.WebApi.Models;
using Dwarf.Rendering;
using Dwarf.Rendering.Renderer2D.Components;
using Microsoft.Extensions.Logging;

namespace Dwarf.Networking.WebApi.Services;

public class MeshService : IMeshService {
  // private readonly ITagRepository _tagRepository;
  private readonly ILogger<IMeshService> _logger;
  private readonly Application _app;

  public MeshService(ILogger<IMeshService> logger) {
    _logger = logger;
    _app = Application.Instance;
  }

  /// <summary>
  /// Retruns the first found level entity
  /// </summary>
  public VertexResponse[] Get2DLevelMesh() {
    var level = _app.GetEntitiesEnumerable()
      .Where(
        x => x.Name.Contains("level") ||
        x.Name.Contains("lvl") ||
        x.Name.Contains("tilemap")
      )
      .FirstOrDefault();

    if (level == null) {
      _logger.LogWarning("level is null");
      return [];
    }

    var tilemap = level.TryGetComponent<Tilemap>();
    if (tilemap is null) {
      _logger.LogWarning("tilemap is null");
      return [];
    }

    var collMesh = tilemap.CollisionMesh?.Vertices;
    if (collMesh is null) {
      _logger.LogWarning("mesh is null");
    }

    return collMesh.ToVertexResponseArray() ?? [];
  }
}