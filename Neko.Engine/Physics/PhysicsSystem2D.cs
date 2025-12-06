using Neko.EntityComponentSystem;
using Neko.Physics.Backends;
using Neko.Physics.Backends.Box2D;
using Neko.Physics.Backends.Hammer;

namespace Neko.Physics;

public class PhysicsSystem2D : IDisposable {
  public IPhysicsProgram PhysicsProgram { get; private set; }

  public PhysicsSystem2D(BackendKind backendKind) {
    PhysicsProgram = backendKind switch {
      BackendKind.Hammer => new HammerProgram(),
      BackendKind.Box2D => new Box2DProgram(),
      _ => new HammerProgram()
    };
  }

  public void Init(Span<Entity> entities) {
    var diff = entities.ToArray().Where(e => e.HasComponent<Rigidbody2D>()).ToArray();
    PhysicsProgram?.Init(entities);
  }

  public void Tick(ReadOnlySpan<Rigidbody2D> rigidbodies2D) {
    for (short i = 0; i < rigidbodies2D.Length; i++) {
      if (rigidbodies2D[i].Owner!.CanBeDisposed) continue;
      rigidbodies2D[i]?.Update();
    }

    PhysicsProgram.Update();
  }

  public void Dispose() {
    PhysicsProgram?.Dispose();
    GC.SuppressFinalize(this);
  }
}