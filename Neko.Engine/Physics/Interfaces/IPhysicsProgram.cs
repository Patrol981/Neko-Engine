using Neko.EntityComponentSystem;

namespace Neko.Physics;

public interface IPhysicsProgram : IDisposable {
  void Init(Span<Entity> entities);
  void Update();
}