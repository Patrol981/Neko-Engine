using Neko.EntityComponentSystem;
using Neko.Extensions.Logging;
using Neko.Physics.Backends;
using Neko.Physics.Backends.Hammer;
using Neko.Physics.Backends.Jolt;

namespace Neko.Physics;

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
    var diff = entities.ToArray().Where(e => e.HasComponent<Rigidbody>()).ToArray();
    PhysicsProgram?.Init(diff);
  }

  public void Tick(ReadOnlySpan<Rigidbody> rigidbodies) {
    for (short i = 0; i < rigidbodies.Length; i++) {
      if (rigidbodies[i].Owner.CanBeDisposed) continue;
      rigidbodies[i].Update();
    }

    PhysicsProgram?.Update();
  }

  public void Dispose() {
    PhysicsProgram?.Dispose();
    GC.SuppressFinalize(this);
  }
}
