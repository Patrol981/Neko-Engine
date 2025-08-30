using Dwarf.EntityComponentSystem;
using Dwarf.Extensions.Logging;
using Dwarf.Globals;
using Dwarf.Hammer;
using Dwarf.Hammer.Models;

namespace Dwarf.Physics.Backends.Hammer;

public class HammerProgram : IPhysicsProgram {
  private readonly HammerInstance _hammerInstance = null!;
  public Dictionary<Entity, HammerBodyWrapper> Bodies = [];
  public HammerInterface HammerInterface => _hammerInstance.HammerInterface;
  public float DeltaTime = 1.0f / 600.0f;

  public HammerProgram() {
    _hammerInstance = new();
    _hammerInstance.OnContactAdded += OnContactAdded;
    _hammerInstance.OnContactPersisted += OnContactPersisted;
    _hammerInstance.OnContactExit += OnContactExit;
    _hammerInstance.OnTilemapContactPersised += OnTilemapContactPersised;
  }

  public void Init(Span<Entity> entities) {
    foreach (var entity in entities) {
      var wrapper = new HammerBodyWrapper(HammerInterface);
      Bodies.Add(entity, wrapper);
      entity.GetRigidbody2D()?.Init(wrapper);
    }

    HammerInterface.SetGravity(0.01f);
  }

  public void Update() {
    _hammerInstance.HammerWorld.Simulate(DeltaTime);
  }

  public static void OnContactAdded(in BodyId body1, in BodyId body2) {
    var data = HammerBodyWrapper.GetCollisionData(body1, body2);
    if (data.Item1 != null && data.Item2 != null) {
      data.Item1.InvokeCollision(CollisionState.Enter, data.Item2.Owner, data.Item2.IsTrigger);
      data.Item2.InvokeCollision(CollisionState.Enter, data.Item1.Owner, data.Item1.IsTrigger);
    }
  }

  public static void OnContactPersisted(in BodyId body1, in BodyId body2) {
    var data = HammerBodyWrapper.GetCollisionData(body1, body2);
    if (data.Item1 != null && data.Item2 != null) {
      data.Item1.InvokeCollision(CollisionState.Stay, data.Item2.Owner, data.Item2.IsTrigger);
      data.Item2.InvokeCollision(CollisionState.Stay, data.Item1.Owner, data.Item1.IsTrigger);
    }
  }

  public static void OnContactExit(in BodyId body1, in BodyId body2) {
    var data = HammerBodyWrapper.GetCollisionData(body1, body2);
    if (data.Item1 != null || data.Item2 != null) {
      data.Item1?.InvokeCollision(CollisionState.Exit, data.Item2?.Owner, data.Item2?.IsTrigger ?? false);
      data.Item2?.InvokeCollision(CollisionState.Exit, data.Item1?.Owner, data.Item1?.IsTrigger ?? false);
    }
  }

  public static void OnTilemapContactPersised(in BodyId body1) {
    var data = HammerBodyWrapper.GetCollisionData(body1);
    data?.InvokeCollision(CollisionState.Stay, null, false);
  }

  public void Dispose() {

  }
}