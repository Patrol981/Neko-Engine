using Dwarf.EntityComponentSystem;

namespace Dwarf.Rendering;

public class RenderTarget {
  private List<Entity> _entities;
  private Camera _renderTargetCamera;

  public RenderTarget() {
    _entities = [];
    _renderTargetCamera = new Camera(null!, 50, 1);
  }

  public void AddRenderTarget(Entity entity) {
    _entities.Add(entity);
  }

  public void AddRenderTargets(Entity[] entities) {
    _entities.AddRange(entities);
  }

  public void Clear() {
    _entities = [];
  }
}