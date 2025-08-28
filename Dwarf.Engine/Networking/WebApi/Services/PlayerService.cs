using Dwarf.Networking.WebApi.Interfaces;
using Microsoft.Extensions.Logging;

namespace Dwarf.Networking.WebApi.Services;

public class PlayerService : IPlayerService {
  private readonly ILogger<IPlayerService> _logger;
  private readonly Application _app;

  public PlayerService(ILogger<IPlayerService> logger) {
    _logger = logger;
    _app = Application.Instance;
  }

  public Guid[] ListEntityIndices() {
    return [.. _app.GetEntitiesEnumerable().Select(x => x.EntityID)];
  }

  public Task AddEntity() {
    return Task.CompletedTask;
  }
}