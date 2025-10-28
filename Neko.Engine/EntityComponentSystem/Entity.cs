using Neko.Extensions.Logging;
using Neko.Physics;
using Neko.Procedural;
using Neko.Rendering.Renderer2D.Interfaces;
using Neko.Rendering.Renderer3D;
using Neko.Rendering.Renderer3D.Animations;

namespace Neko.EntityComponentSystem;

public class Entity {
  public string Name;
  public Guid Id;
  public Dictionary<Type, Guid> Components;

  public bool Active { get; set; }
  public bool CanBeDisposed { get; set; }
  public bool IsImportant { get; set; }
  public EntityLayer Layer { get; set; }
  internal bool Collected { get; set; }

  public Entity(string name) {
    Name = name;
    Id = Guid.NewGuid();
    Components = [];
    CanBeDisposed = false;
    IsImportant = false;
    Active = true;
    Collected = false;
    Layer = EntityLayer.Default;
  }

  public void Dispose(Application app) {
    foreach (var comp in Components) {
      Type key = comp.Key;
      Guid id = comp.Value;

      if (typeof(NekoScript).IsAssignableFrom(key)) {
        if (app.Scripts.TryGetValue(id, out var NekoScript)) {
          NekoScript.Dispose();
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
      } else if (key == typeof(IRender3DElement)) {
        if (app.Drawables3D.TryGetValue(id, out var renderable)) {
          renderable.Dispose();
          app.Drawables3D.Remove(id, out _);
        }
      } else if (key == typeof(MaterialComponent)) {
        if (app.Materials.TryGetValue(id, out var material)) {
          app.Materials.Remove(id, out _);
        }
      } else if (key == typeof(AnimationController)) {
        if (app.AnimationControllers.TryGetValue(id, out var animationController)) {
          app.AnimationControllers.Remove(id, out _);
        }
      } else if (key == typeof(Rigidbody)) {
        if (app.Rigidbodies.TryGetValue(id, out var rigidbody)) {
          rigidbody.Dispose();
          app.Rigidbodies.Remove(id, out _);
        }
      } else if (key == typeof(Terrain3D)) {
        if (app.TerrainMeshes.TryGetValue(id, out var terrain)) {
          app.TerrainMeshes.Remove(id, out _);
        }
      } else if (key == typeof(PointLightComponent)) {
        if (app.Lights.TryGetValue(id, out var light)) {
          app.Lights.Remove(id, out _);
        }
      }
    }
  }
}