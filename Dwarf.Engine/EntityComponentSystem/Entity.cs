using Dwarf.Physics;
using Dwarf.Rendering.Renderer2D.Interfaces;

namespace Dwarf.EntityComponentSystem;

public class Entity {
  public string Name;
  public Guid Id;
  public Dictionary<Type, Guid> Components;

  public bool Active { get; set; }
  public bool CanBeDisposed { get; set; }
  public bool IsImportant { get; set; }
  internal bool Collected { get; set; }

  public Entity(string name) {
    Name = name;
    Id = Guid.NewGuid();
    Components = [];
    CanBeDisposed = false;
    IsImportant = false;
    Active = true;
    Collected = false;
  }

  internal void Dispose(Application app) {
    foreach (var comp in Components) {
      Type key = comp.Key;
      Guid id = comp.Value;

      if (key == typeof(DwarfScript)) {
        if (app.Scripts.TryGetValue(id, out var dwarfScript)) {
          dwarfScript.Dispose();
          app.Scripts.Remove(id, out _);
        }
      } else if (key == typeof(TransformComponent)) {
        if (app.TransformComponents.TryGetValue(id, out _)) {
          app.TransformComponents.Remove(id, out _);
        }
      } else if (key == typeof(IDrawable2D)) {
        if (app.Sprites.TryGetValue(id, out var drawable2D)) {
          drawable2D.Dispose();
          app.Sprites.Remove(id, out _);
        }
      } else if (key == typeof(Rigidbody2D)) {
        if (app.Rigidbodies2D.TryGetValue(id, out var rigidbody2D)) {
          rigidbody2D.Dispose();
          app.Rigidbodies2D.Remove(id, out _);
        }
      } else if (key == typeof(ColliderMesh)) {
        if (app.DebugMeshes.TryGetValue(id, out var colliderMesh)) {
          colliderMesh.Dispose();
          app.DebugMeshes.Remove(id, out _);
        }
      }
    }
  }
}