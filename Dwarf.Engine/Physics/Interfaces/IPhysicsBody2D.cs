using System.Numerics;
using Dwarf.EntityComponentSystem;
using Dwarf.Rendering;

namespace Dwarf.Physics.Interfaces;

public interface IPhysicsBody2D : IDisposable {
  Vector2 Position { get; set; }
  Vector2 LinearVelocity { get; set; }
  Vector2 AngularVelocity { get; set; }
  float GravityFactor { get; set; }
  MotionQuality MotionQuality { get; set; }
  MotionType MotionType { get; set; }
  bool Grounded { get; }
  object CreateAndAddBody(object settings);
  object ColldierMeshToPhysicsShape(Entity entity, Mesh colliderMesh);
  object BodyId { get; }
  void CreateAndAddBody(MotionType motionType, object shapeSettings, Vector2 position, bool isTrigger);
  void RemoveBody();
  void SetActive(bool value);
  void AddForce(Vector2 force);
  void AddLinearVelocity(Vector2 velocity);
  void AddImpulse(Vector2 impulse);
}