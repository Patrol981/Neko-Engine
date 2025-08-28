using Dwarf.EntityComponentSystemRewrite;

namespace Dwarf.Physics;

public interface IPhysicsProgram : IDisposable {
  void Init(Span<Entity> entities);
  void Update();
}