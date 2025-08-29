using Dwarf.EntityComponentSystem;

namespace Dwarf.Physics;

public interface IPhysicsProgram : IDisposable {
  void Init(Span<Entity> entities);
  void Update();
}