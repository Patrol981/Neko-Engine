using JoltPhysicsSharp;

namespace Neko.Physics;

public interface IBodyInterface {
  // Jolt
  BodyID CreateAndAddBody(BodyCreationSettings settings, JoltPhysicsSharp.Activation activation);
  void SetGravityFactor(in BodyID bodyId, float gravityFactor);
  void SetMotionQuality(in BodyID bodyId, MotionQuality quality);


}