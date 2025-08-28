using System.Numerics;
using Dwarf.EntityComponentSystem;
using Dwarf.Loaders.Tiled;
using Dwarf.Physics;
using Dwarf.Rendering.Renderer2D.Components;
using Dwarf.Rendering.Renderer2D.Interfaces;
using ZLinq;

namespace Dwarf.EntityComponentSystemRewrite;

public static class EntityExtensions {
  public static TransformComponent? GetTransform(this Entity entity) {
    if (entity.CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");
    if (entity.Components.TryGetValue(typeof(TransformComponent), out var guid)) {
      return Application.Instance.TransformComponents[guid];
    } else {
      return null;
    }
  }

  public static unsafe void AddTransform(this Entity entity, TransformComponent transformComponent) {
    if (entity.CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");
    var guid = Guid.NewGuid();
    try {
      entity.Components.TryAdd(typeof(TransformComponent), guid);
      if (!Application.Instance.TransformComponents.TryAdd(guid, transformComponent)) {
        throw new Exception("Cannot add transform to list");
      }
    } catch {
      throw;
    }
  }

  public static IDrawable2D? GetDrawable2D(this Entity entity) {
    if (entity.CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");
    if (entity.Components.TryGetValue(typeof(IDrawable2D), out var guid)) {
      return Application.Instance.Sprites[guid];
    } else {
      return null;
    }
  }

  public static Entity AddDrawable2D(this Entity entity, IDrawable2D drawable2D) {
    if (entity.CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");
    var guid = Guid.NewGuid();
    try {
      entity.Components.TryAdd(typeof(IDrawable2D), guid);
      if (!Application.Instance.Sprites.TryAdd(guid, drawable2D)) {
        throw new Exception("Cannot add transform to list");
      }
      return entity;
    } catch {
      throw;
    }
  }

  public static ReadOnlySpan<IDrawable2D> FlattenDrawable2D(this HashSet<Entity> entities) {
    var buffer = new List<IDrawable2D>();

    for (int i = 0; i < entities.Count; i++) {
      var e = entities.ElementAt(i);
      if (e.CanBeDisposed)
        continue;

      var drawable = e.GetDrawable2D();
      if (drawable == null) continue;

      if (drawable.Children.Length > 0) {
        for (int j = 0; j < drawable.Children.Length; j++) {
          buffer.Add(drawable.Children[j]);
        }
      } else {
        buffer.Add(drawable);
      }
    }

    if (buffer.Count != 0) {
      buffer.Sort(Drawable2DComparer.Instance);
    }

    return buffer.ToArray();
  }

  public static SpriteRenderer.Builder AddSpriteBuilder(this Entity entity) {
    if (entity.CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");
    var app = Application.Instance;
    var builder = new SpriteRenderer.Builder(app, entity);
    return builder;
  }

  public static void AddTileMap(this Entity entity, string tmxPath) {
    if (entity.CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");
    var app = Application.Instance;
    try {
      var tilemap = TiledLoader.LoadTilemap(app, tmxPath);
      tilemap.Entity = entity;
      entity.AddDrawable2D(tilemap);
    } catch {
      throw;
    }
  }


  public static T? GetScript<T>(this Entity entity) where T : EntityComponentSystem.DwarfScript {
    if (entity.CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");
    if (entity.Components.TryGetValue(typeof(EntityComponentSystem.DwarfScript), out var guid)) {
      var result = Application.Instance.Scripts[guid];
      return (T?)result;
    } else {
      return null;
    }
  }

  public static DwarfScript[] GetScripts(this Entity entity) {
    if (entity.CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");

    var app = Application.Instance;

    return entity.Components
      .AsValueEnumerable()
      .Where(kv => typeof(DwarfScript).IsAssignableFrom(kv.Key))
      .Select(kv => app.Scripts.TryGetValue(kv.Value, out var s) ? s : null)
      .Where(s => s is not null)
      .Cast<DwarfScript>()
      .ToArray();
  }

  public static void AddScript(this Entity entity, EntityComponentSystem.DwarfScript script) {
    if (entity.CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");
    var guid = Guid.NewGuid();
    try {
      entity.Components.TryAdd(typeof(EntityComponentSystem.DwarfScript), guid);
      if (!Application.Instance.Scripts.TryAdd(guid, script)) {
        throw new Exception("Cannot add transform to list");
      }
      script.OwnerNew = entity;
    } catch {
      throw;
    }
  }

  public static void AddRigidbody2D(
    this Entity entity,
    PrimitiveType primitiveType,
    MotionType motionType,
    Vector2 min,
    Vector2 max,
    bool isTrigger = false
  ) {
    if (entity.CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");
    var guid = Guid.NewGuid();
    try {
      Application.Mutex.WaitOne();
      entity.Components.Add(typeof(Rigidbody2D), guid);
      var rb2D = new Rigidbody2D(Application.Instance, primitiveType, motionType, min, max, isTrigger) {
        Owner = entity
      };
      if (!Application.Instance.Rigidbodies2D.TryAdd(guid, rb2D)) {
        throw new Exception("Cannot add transform to list");
      }
      Application.Mutex.ReleaseMutex();
    } catch {
      Application.Mutex.ReleaseMutex();
      throw;
    }
  }

  public static Rigidbody2D? GetRigidbody2D(this Entity entity) {
    if (entity.CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");
    if (entity.Components.TryGetValue(typeof(Rigidbody2D), out var guid)) {
      var result = Application.Instance.Rigidbodies2D[guid];
      return result;
    } else {
      return null;
    }
  }

  public static bool HasComponent<T>(this Entity entity) {
    if (entity.CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");
    return entity.Components.ContainsKey(typeof(T));
  }

  private sealed class Drawable2DComparer : IComparer<IDrawable2D> {
    public static readonly Drawable2DComparer Instance = new();
    private Drawable2DComparer() { }

    public int Compare(IDrawable2D? a, IDrawable2D? b) {
      if (a != null && a.Entity.CanBeDisposed) return 0;
      if (b != null && b.Entity.CanBeDisposed) return 0;
      // float az = a!.Entity.TryGetComponent<Transform>()?.Position.Z ?? 0;
      // float bz = b!.Entity.TryGetComponent<Transform>()?.Position.Z ?? 0;

      float az = a!.Entity.GetTransform()?.Position.Z ?? 0;
      float bz = b!.Entity.GetTransform()?.Position.Z ?? 0;
      if (az < bz) return -1;
      if (az > bz) return 1;
      return 0;
    }
  }
}