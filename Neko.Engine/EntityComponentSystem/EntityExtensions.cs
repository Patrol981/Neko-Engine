using System.Numerics;
using Neko.AbstractionLayer;
using Neko.EntityComponentSystem;
using Neko.Loaders;
using Neko.Loaders.Tiled;
using Neko.Physics;
using Neko.Procedural;
using Neko.Rendering;
using Neko.Rendering.Renderer2D.Components;
using Neko.Rendering.Renderer2D.Interfaces;
using Neko.Rendering.Renderer3D;
using Neko.Rendering.Renderer3D.Animations;
using ZLinq;

namespace Neko.EntityComponentSystem;

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

  public static unsafe void AddTransform(
    this Entity entity,
    ReadOnlySpan<float> position
  ) {
    entity.AddTransform(position, [0, 0, 0], [1, 1, 1]);
  }

  public static unsafe void AddTransform(
    this Entity entity,
    ReadOnlySpan<float> position,
    ReadOnlySpan<float> rotation
  ) {
    entity.AddTransform(position, rotation, [1, 1, 1]);
  }

  public static unsafe void AddTransform(
    this Entity entity,
    ReadOnlySpan<float> position,
    ReadOnlySpan<float> rotation,
    float[] scale
  ) {
    if (entity.CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");

    var guid = Guid.NewGuid();
    if (position.Length < 1) {
      position = [0, 0, 0];
    }
    if (rotation.Length < 1) {
      rotation = [0, 0, 0];
    }
    if (scale.Length < 1) {
      scale = [1, 1, 1];
    } else if (scale.Length == 1) {
      scale = [scale[0], scale[0], scale[0]];
    }

    var transform = new TransformComponent(new(position), new(rotation), new(scale));
    try {
      entity.Components.TryAdd(typeof(TransformComponent), guid);
      if (!Application.Instance.TransformComponents.TryAdd(guid, transform)) {
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


  public static T? GetScript<T>(this Entity entity) where T : NekoScript {
    if (entity.CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");
    if (entity.Components.TryGetValue(typeof(T), out var guid)) {
      var result = Application.Instance.Scripts[guid];
      return (T?)result;
    } else {
      return null;
    }
  }

  public static NekoScript[] GetScripts(this Entity entity) {
    if (entity.CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");

    var app = Application.Instance;

    return entity.Components
      .AsValueEnumerable()
      .Where(kv => typeof(NekoScript).IsAssignableFrom(kv.Key))
      .Select(kv => app.Scripts.TryGetValue(kv.Value, out var s) ? s : null)
      .Where(s => s is not null)
      .Cast<NekoScript>()
      .ToArray();
  }
  public static void AddScript<T>(this Entity entity, T script) where T : NekoScript {
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

    if (typeof(NekoScript).IsAssignableFrom(data?.GetType())) {
      entity.AddScript((data as NekoScript)!);
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

  public static IRender3DElement WithCustomShader(this IRender3DElement mesh, string shaderName) {
    mesh.SetCustomShader(new(shaderName));

    return mesh;
  }

  public static IRender3DElement WithAdditionalTexture(this IRender3DElement mesh, Guid textureId) {
    mesh.SetShaderTextureInfo(textureId);

    return mesh;
  }

  public static IRender3DElement WithAdditionalTexture(this IRender3DElement mesh, ITexture texture) {
    var textureId = Application.Instance.TextureManager.GetTextureIdLocal(texture.TextureName);
    mesh.SetShaderTextureInfo(textureId);

    return mesh;
  }

  public static IRender3DElement WithAdditionalTexture(this IRender3DElement mesh, string texturePath) {
    var textureManager = Application.Instance.TextureManager;
    var texture = textureManager.AddTextureLocal(texturePath).Result;
    var textureId = textureManager.GetTextureIdLocal(texture.TextureName);
    mesh.SetShaderTextureInfo(textureId);

    return mesh;
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