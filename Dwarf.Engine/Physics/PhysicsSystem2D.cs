using Dwarf.EntityComponentSystemRewrite;
using Dwarf.Physics.Backends;
using Dwarf.Physics.Backends.Hammer;

namespace Dwarf.Physics;

public class PhysicsSystem2D : IDisposable {
  public IPhysicsProgram PhysicsProgram { get; private set; }

  public PhysicsSystem2D(BackendKind backendKind) {
    PhysicsProgram = backendKind switch {
      BackendKind.Hammer => new HammerProgram(),
      _ => new HammerProgram()
    };
  }

  public void Init(Span<Entity> entities) {
    var diff = entities.ToArray().Where(e => e.HasComponent<Rigidbody2D>()).ToArray();
    PhysicsProgram?.Init(entities);
  }

  public void Tick(Entity[] entities) {
    for (short i = 0; i < entities.Length; i++) {
      if (entities[i].CanBeDisposed) continue;
      entities[i].GetRigidbody2D()?.Update();
    }

    PhysicsProgram.Update();
  }

  public void Dispose() {
    PhysicsProgram?.Dispose();
    GC.SuppressFinalize(this);
  }
}