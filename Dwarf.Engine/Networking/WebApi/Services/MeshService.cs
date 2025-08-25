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
  public MeshResponse Get2DLevelMesh() {
    var meshResponse = new MeshResponse();

    var level = _app.GetEntitiesEnumerable()
      .Where(
        x => x.Name.Contains("level") ||
        x.Name.Contains("lvl") ||
        x.Name.Contains("tilemap")
      )
      .FirstOrDefault();

    if (level == null) {
      _logger.LogWarning("level is null");
      return meshResponse;
    }

    var tilemap = level.TryGetComponent<Tilemap>();
    if (tilemap is null) {
      _logger.LogWarning("tilemap is null");
      return meshResponse;
    }

    var collMesh = tilemap.CollisionMesh?.Vertices;
    if (collMesh is null) {
      _logger.LogWarning("mesh is null");
    }

    var scale = level.GetComponent<Transform>().Scale;

    var vertices = collMesh?.ToVertexResponseArray() ?? [];
    var indices = tilemap.CollisionMesh?.Indices ?? [];

    foreach (var vert in vertices) {
      vert!.Position![0] *= scale.X;
      vert!.Position![1] *= scale.Y;
    }

    meshResponse.Vertices = vertices;
    meshResponse.Indices = indices;

    return meshResponse;
  }
}