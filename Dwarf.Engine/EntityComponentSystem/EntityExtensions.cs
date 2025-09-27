using System.Numerics;
using Dwarf.EntityComponentSystem;
using Dwarf.Loaders;
using Dwarf.Loaders.Tiled;
using Dwarf.Physics;
using Dwarf.Procedural;
using Dwarf.Rendering;
using Dwarf.Rendering.Renderer2D.Components;
using Dwarf.Rendering.Renderer2D.Interfaces;
using Dwarf.Rendering.Renderer3D;
using Dwarf.Rendering.Renderer3D.Animations;
using ZLinq;

namespace Dwarf.EntityComponentSystem;

public static class EntityExtensions {
  public static TransformComponent? GetTransform(this Entity entity) {
    // if (entity.CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");
    if (entity.CanBeDisposed) {
      return null;
    }
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

  public static MaterialComponent? GetMaterial(this Entity entity) {
    if (entity.CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");
    if (entity.Components.TryGetValue(typeof(MaterialComponent), out var guid)) {
      var result = Application.Instance.Materials[guid];
      return result;
    } else {
      return null;
    }
  }

  public static void AddMaterial(this Entity entity, MaterialComponent material) {
    if (entity.CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");
    var guid = Guid.NewGuid();
    try {
      entity.Components.TryAdd(typeof(MaterialComponent), guid);
      if (!Application.Instance.Materials.TryAdd(guid, material)) {
        throw new Exception("Cannot add material to list");
      }
    } catch {
      throw;
    }
  }

  public static void AddMaterial(this Entity entity) {
    if (entity.CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");
    var guid = Guid.NewGuid();
    try {
      entity.Components.TryAdd(typeof(MaterialComponent), guid);
      var material = new MaterialComponent(entity.Id, Vector3.One);
      if (!Application.Instance.Materials.TryAdd(guid, material)) {
        throw new Exception("Cannot add material to list");
      }
    } catch {
      throw;
    }
  }

  public static void AddMaterial(this Entity entity, MaterialData materialData) {
    if (entity.CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");
    var guid = Guid.NewGuid();
    try {
      entity.Components.TryAdd(typeof(MaterialComponent), guid);
      var material = new MaterialComponent(materialData);
      if (!Application.Instance.Materials.TryAdd(guid, material)) {
        throw new Exception("Cannot add material to list");
      }
    } catch {
      throw;
    }
  }

  public static IRender3DElement? GetDrawable3D(this Entity entity) {
    if (entity.CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");
    if (entity.Components.TryGetValue(typeof(IRender3DElement), out var guid)) {
      return Application.Instance.Drawables3D[guid];
    } else {
      return null;
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

  public static Entity AddDrawable3D(this Entity entity, IRender3DElement renderable) {
    if (entity.CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");
    var guid = Guid.NewGuid();
    try {
      entity.Components.TryAdd(typeof(IRender3DElement), guid);
      if (!Application.Instance.Drawables3D.TryAdd(guid, renderable)) {
        throw new Exception("Cannot add transform to list");
      }
      return entity;
    } catch {
      throw;
    }
  }

  public static Entity AddTerrain(this Entity entity, Terrain3D terrain) {
    if (entity.CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");
    var guid = Guid.NewGuid();
    try {
      entity.Components.TryAdd(typeof(Terrain3D), guid);
      if (!Application.Instance.TerrainMeshes.TryAdd(guid, terrain)) {
        throw new Exception("Cannot add transform to list");
      }
      return entity;
    } catch {
      throw;
    }
  }

  public static Terrain3D? GetTerrain(this Entity entity) {
    if (entity.CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");
    if (entity.Components.TryGetValue(typeof(Terrain3D), out var guid)) {
      return Application.Instance.TerrainMeshes[guid];
    } else {
      return null;
    }
  }

  public static async void AddModel(
    this Entity entity,
    string modelPath,
    int flip = 0,
    bool useRig = true
  ) {
    var app = Application.Instance;

    if (!modelPath.Contains("glb")) {
      throw new Exception("This method does not support formats other than .glb");
    }

    entity.AddDrawable3D(await GLTFLoaderKHR.LoadGLTF(entity, app, modelPath, flip));
    var meshRenderer = entity.GetDrawable3D()!;
    if (meshRenderer.Animations.Count > 0 && useRig) {
      entity.AddAnimationController(
        new AnimationController(
          entity,
          [.. meshRenderer.Animations],
          (meshRenderer as MeshRenderer)!
        )
      );
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
      var tilemap = TiledLoader.LoadTilemap(entity, app, tmxPath);
      entity.AddDrawable2D(tilemap);
    } catch {
      throw;
    }
  }


  public static T? GetScript<T>(this Entity entity) where T : DwarfScript {
    if (entity.CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");
    if (entity.Components.TryGetValue(typeof(T), out var guid)) {
      var result = Application.Instance.Scripts[guid];
      return (T?)result;
    } else {
      return null;
    }
  }

  // public static T? GetScript<T>(this Entity entity) where T : DwarfScript {
  //   if (entity.CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");
  //   if (entity.Components.TryGetValue(typeof(DwarfScript[]), out var guid)) {
  //     var result = Application.Instance.Scripts[guid].Where(x => x.GetType() == typeof(T)).First();
  //     return (T?)result;
  //   } else {
  //     return null;
  //   }
  // }

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

  //   public static void AddScript(this Entity entity, DwarfScript script) {
  //   if (entity.CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");
  //   var guid = Guid.Empty;
  //   var app = Application.Instance;

  //   if (entity.Components.ContainsKey(typeof(DwarfScript))) {
  //     guid = entity.Components.Where(x => x.Key.GetType() == typeof(DwarfScript)).First().Value;
  //   } else {
  //     guid = Guid.NewGuid();
  //   }

  //   try {
  //     entity.Components.TryAdd(typeof(DwarfScript), guid);
  //     if (app.Scripts.ContainsKey(guid)) {
  //       app.Scripts[guid].Add(script);
  //     } else {
  //       app.Scripts.TryAdd(guid, [script]);
  //     }

  //     script.Owner = entity;
  //   } catch {
  //     throw;
  //   }
  // }

  public static void AddScript<T>(this Entity entity, T script) where T : DwarfScript {
    if (entity.CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");
    var guid = Guid.NewGuid();
    try {
      entity.Components.TryAdd(typeof(T), guid);
      if (!Application.Instance.Scripts.TryAdd(guid, script)) {
        throw new Exception("Cannot add transform to list");
      }
      script.Owner = entity;
    } catch {
      throw;
    }
  }

  public static ColliderMesh? GetColliderMesh(this Entity entity) {
    if (entity.CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");
    if (entity.Components.TryGetValue(typeof(ColliderMesh), out var guid)) {
      var result = Application.Instance.DebugMeshes[guid];
      return result;
    } else {
      return null;
    }
  }

  public static Camera? GetCamera(this Entity entity) {
    if (entity.CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");
    var result = Application.Instance.CameraComponent;
    return result;
  }

  public static void AddCamera(this Entity entity, Camera camera) {
    if (entity.CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");
    Application.Instance.CameraEntity = entity;
    Application.Instance.CameraComponent = camera;
  }

  public static void AddAnimationController(this Entity entity, AnimationController animationController) {
    if (entity.CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");
    var guid = Guid.NewGuid();
    try {
      entity.Components.TryAdd(typeof(AnimationController), guid);
      if (!Application.Instance.AnimationControllers.TryAdd(guid, animationController)) {
        throw new Exception("Cannot add transform to list");
      }
    } catch {
      throw;
    }
  }

  public static AnimationController? GetAnimationController(this Entity entity) {
    if (entity.CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");
    if (entity.Components.TryGetValue(typeof(AnimationController), out var guid)) {
      var result = Application.Instance.AnimationControllers[guid];
      return result;
    } else {
      return null;
    }
  }

  public static bool HasComponent<T>(this Entity entity) {
    if (entity.CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");
    return entity.Components.ContainsKey(typeof(T));
  }

  public static bool HasAndImplementComponent<T1, T2>(
    this Entity entity, T2? data
  ) where T1 : class {
    if (entity.CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");
    if (typeof(T2).IsAssignableFrom(typeof(T1))) {
      return data is T1;
    }
    return false;
  }

  public static void AddComponent<T>(this Entity entity, T data) {
    if (entity.CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");
    var guid = Guid.NewGuid();
    var app = Application.Instance;

    if (typeof(DwarfScript).IsAssignableFrom(data?.GetType())) {
      entity.AddScript((data as DwarfScript)!);
      return;
    }

    try {
      switch (data) {
        case ColliderMesh colliderMesh:
          entity.Components.Add(typeof(T), guid);
          app.DebugMeshes.TryAdd(guid, colliderMesh);
          return;
        default:
          throw new ArgumentException("AddComponent does not support this type", typeof(T).ToString());
      }
    } catch {
      throw;
    }
  }

  private sealed class Drawable2DComparer : IComparer<IDrawable2D> {
    public static readonly Drawable2DComparer Instance = new();
    private Drawable2DComparer() { }

    public int Compare(IDrawable2D? a, IDrawable2D? b) {
      if (a != null && a.Entity.CanBeDisposed) return 0;
      if (b != null && b.Entity.CanBeDisposed) return 0;

      float az = a!.Entity.GetTransform()?.Position.Z ?? 0;
      float bz = b!.Entity.GetTransform()?.Position.Z ?? 0;

      if (az < bz) return -1;
      if (az > bz) return 1;

      return 0;
    }
  }
}