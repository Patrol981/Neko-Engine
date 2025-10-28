using System.Collections.Concurrent;
using Neko.AbstractionLayer;
using Neko.Animations;
using Neko.EntityComponentSystem;
using Neko.Physics;
using Neko.Procedural;
using Neko.Rendering;
using Neko.Rendering.Renderer2D.Components;
using Neko.Rendering.Renderer2D.Interfaces;
using Neko.Rendering.Renderer3D;
using Neko.Rendering.Renderer3D.Animations;

namespace Neko;

public partial class Application {
  public HashSet<Entity> Entities = [];
  internal ConcurrentDictionary<Guid, NekoScript> Scripts = [];
  internal ConcurrentDictionary<Guid, TransformComponent> TransformComponents = [];
  internal ConcurrentDictionary<Guid, IDrawable2D> Sprites = [];
  internal ConcurrentDictionary<Guid, Rigidbody2D> Rigidbodies2D = [];
  internal ConcurrentDictionary<Guid, ColliderMesh> DebugMeshes = [];
  internal ConcurrentDictionary<Guid, IRender3DElement> Drawables3D = [];
  internal ConcurrentDictionary<Guid, MaterialComponent> Materials = [];
  internal ConcurrentDictionary<Guid, AnimationController> AnimationControllers = [];
  internal ConcurrentDictionary<Guid, Rigidbody> Rigidbodies = [];
  internal ConcurrentDictionary<Guid, Terrain3D> TerrainMeshes = [];

  internal ConcurrentDictionary<Guid, PointLightComponent> Lights = [];

  // internal ConcurrentDictionary<Guid, Camera> Cameras = [];

  internal ConcurrentDictionary<Guid, Mesh> Meshes = [];
  internal ConcurrentDictionary<Guid, Skin> Skins = [];
  internal ConcurrentDictionary<Guid, AnimationNode> AnimationNodes = [];

  // Computed 
  // internal ConcurrentDictionary

  private readonly Queue<Entity> _entitiesQueue = new();
  private readonly Queue<MeshRenderer> _reloadQueue = new();
  public readonly Lock EntitiesLock = new();

  public void AddEntity(Entity entity, bool fenced = false) {
    Mutex.WaitOne();
    var scripts = entity.GetScripts();
    MasterAwake(scripts);
    MasterStart(scripts);
    if (fenced) {
      var fence = Device.CreateFence(FenceCreateFlags.Signaled);
      Device.WaitFence(fence, true);
    }
    Entities.Add(entity);
    Mutex.ReleaseMutex();
  }

  public void RemoveEntity(Guid id) {
    Entities.RemoveWhere(x => x.Id == id);
  }

  public Entity? GetEntity(Guid entitiyId) {
    lock (EntitiesLock) {
      return Entities.Where(x => x.Id == entitiyId).First();
    }
  }

  public void RemoveEntityAt(int index) {
    lock (EntitiesLock) {
      Device.WaitDevice();
      Device.WaitQueue();
      Entities.Remove(Entities.ElementAt(index));
    }
  }

  public void RemoveEntity(Entity entity) {
    lock (EntitiesLock) {
      Device.WaitDevice();
      Device.WaitQueue();
      Entities.Remove(entity);
    }
  }

  public void DestroyEntity(Entity entity) {
    lock (EntitiesLock) {
      entity.CanBeDisposed = true;
    }
  }

  public void AddEntities(Entity[] entities) {
    foreach (var entity in entities) {
      AddEntity(entity);
    }
  }

  public ReadOnlySpan<Entity> GetEntities() {
    lock (EntitiesLock) {
      return Entities.Where(x => !x.CanBeDisposed).ToArray();
    }
  }

  public List<Entity> GetEntitiesList() {
    lock (EntitiesLock) {
      return [.. Entities.Where(x => !x.CanBeDisposed)];
    }
  }

  public IEnumerable<Entity> GetEntitiesEnumerable() {
    lock (EntitiesLock) {
      return [.. Entities.Where(x => !x.CanBeDisposed)];
    }
  }

  public void AddModelToReloadQueue(MeshRenderer meshRenderer) {
    _reloadQueue.Enqueue(meshRenderer);
  }

  private void Collect() {
    if (Entities.Count == 0) return;
    for (short i = 0; i < Entities.Count; i++) {
      var target = Entities.ElementAt(i);
      if (target.CanBeDisposed) {
        if (target.Collected) continue;

        target.Collected = true;
        target.Dispose(this);
        RemoveEntity(target.Id);
      }
    }
  }


  //// Legacy Code 

  //   public void RemoveEntity(Guid id) {
  //   lock (EntitiesLock) {
  //     if (_entities.Count == 0) return;
  //     var target = _entities.Where((x) => x.EntityID == id).FirstOrDefault();
  //     if (target == null) return;
  //     Device.WaitDevice();
  //     Device.WaitQueue();
  //     _entities.Remove(target);
  //   }
  // }


  //   private void CollectLegacy() {
  //   if (_entities.Count == 0) return;
  //   for (short i = 0; i < _entities.Count; i++) {
  //     if (_entities[i].CanBeDisposed) {
  //       if (_entities[i].Collected) continue;

  //       _entities[i].Collected = true;
  //       _entities[i].DisposeEverything();
  //       RemoveEntity(_entities[i].EntityID);
  //     }
  //   }
  // }

  //   public void AddEntity(Entity entity, bool fenced = false) {
  //   Mutex.WaitOne();
  //   MasterAwake(new[] { entity }.GetScriptsAsSpan());
  //   MasterStart(new[] { entity }.GetScriptsAsSpan());
  //   if (fenced) {
  //     var fence = Device.CreateFence(FenceCreateFlags.Signaled);
  //     Device.WaitFence(fence, true);
  //   }
  //   _entitiesQueue.Enqueue(entity);
  //   Mutex.ReleaseMutex();
  // }

}