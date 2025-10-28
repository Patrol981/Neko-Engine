using System.Numerics;
using Neko;
using Neko.Physics;

namespace Neko.EntityComponentSystem;

public static class EntityBuilder {
  public class CollisionBuilder(string name = "coll") {
    private List<(Vector3 Size, Vector3 Offset)> _collisionPoints = [];
    private readonly string _collName = name;

    public CollisionBuilder AddCollision(Vector3 size, Vector3 offset) {
      _collisionPoints.Add((size, offset));
      return this;
    }

    public ReadOnlySpan<Entity> Build() {
      Entity[] entities = new Entity[_collisionPoints.Count];
      for (int i = 0; i < _collisionPoints.Count; i++) {
        entities[i] = new Entity($"{_collName}-{i}");
        entities[i].AddTransform(new TransformComponent());
        entities[i].AddRigidbody(
          primitiveType: PrimitiveType.Box,
          _collisionPoints[i].Size,
          _collisionPoints[i].Offset,
          motionType: MotionType.Static,
          flip: false
        );
      }
      return entities;
    }
  }
}