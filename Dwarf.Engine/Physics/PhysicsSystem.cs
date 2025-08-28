using Dwarf.EntityComponentSystem;
using Dwarf.Extensions.Logging;
using Dwarf.Physics.Backends;
using Dwarf.Physics.Backends.Hammer;
using Dwarf.Physics.Backends.Jolt;

namespace Dwarf.Physics;

public class PhysicsSystem : IDisposable {
  public IPhysicsProgram PhysicsProgram { get; private set; }

  public PhysicsSystem(BackendKind backendKind) {
    Logger.Info($"[CREATING PHYSICS 3D]");
    PhysicsProgram = backendKind switch {
      BackendKind.Jolt => new JoltProgram(),
      _ => new JoltProgram(),
    };
  }

  public void Init(Span<Entity> entities) {
    // var diff = entities.ToArray().Where(e => e.HasComponent<Rigidbody>()).ToArray();
    // PhysicsProgram?.Init(diff);
  }

  public void Tick(Entity[] entities) {
    for (short i = 0; i < entities.Length; i++) {
      if (entities[i].CanBeDisposed) continue;
      entities[i].GetComponent<Rigidbody>()?.Update();
    }

    PhysicsProgram?.Update();
  }

  public void Dispose() {
    PhysicsProgram?.Dispose();
    GC.SuppressFinalize(this);
  }
}
