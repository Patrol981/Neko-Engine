using System.Numerics;
using Dwarf.Math;

namespace Dwarf.EntityComponentSystemRewrite;

public static class TransformComponentExtensions {
  public static Matrix4x4 OverrideZDepth(this Matrix4x4 mat4, float z) {
    return Matrix4x4.CreateTranslation(0, 0, z) * mat4;
  }

  /// <summary>
  /// Sets Transform euler angle to given position
  /// </summary>
  /// <param name="position"></param>
  public static void LookAt(this TransformComponent transform, Vector3 position) {
    var direction = position - transform.Position;
    direction = Vector3.Normalize(direction);
    var yaw = MathF.Atan2(-direction.X, -direction.Z);
    yaw = Converter.RadiansToDegrees(yaw);
    var pitch = MathF.Asin(direction.Y);
    pitch = -Converter.RadiansToDegrees(pitch);
    transform.Rotation.Y = yaw;
    transform.Rotation.X = pitch;
  }

  /// <summary>
  /// Sets Transform euler angle to given position only by Y axis
  /// </summary>
  /// <param name="position"></param>
  public static void LookAtFixed(this TransformComponent transform, Vector3 position) {
    var direction = position - transform.Position;
    direction.Y = 0;
    direction = Vector3.Normalize(direction);
    var yaw = MathF.Atan2(-direction.X, -direction.Z);
    yaw = Converter.RadiansToDegrees(yaw);
    transform.Rotation.Y = yaw;
  }

  /// <summary>
  /// Sets Transform euler angle to given position only by Z axis
  /// </summary>
  /// <param name="position"></param>
  public static void LookAtFixed2D(this TransformComponent transform, Vector2 position) {
    var direction = position - transform.Position.ToVector2();
    direction = Vector2.Normalize(direction);
    var angle = MathF.Atan2(direction.X, direction.Y);
    angle = Converter.RadiansToDegrees(angle);
    transform.Rotation.Z = angle;
  }

  /// <summary>
  /// Sets Transform euler angle to given position only by Y axis
  /// </summary>
  /// <param name="position"></param>
  public static void LookAtFixed(this TransformComponent transform, Vector3 position, float lerpFactor) {
    // Calculate the target direction
    var direction = position - transform.Position;
    direction.Y = 0;
    direction = Vector3.Normalize(direction);

    // Calculate the target yaw angle
    var targetYaw = MathF.Atan2(-direction.X, -direction.Z);
    targetYaw = Converter.RadiansToDegrees(targetYaw);

    // Normalize both angles to the range [0, 360)
    var currentYaw = NormalizeAngle(transform.Rotation.Y);
    targetYaw = NormalizeAngle(targetYaw);

    // Calculate the shortest direction to rotate
    var deltaYaw = targetYaw - currentYaw;
    if (deltaYaw > 180) {
      deltaYaw -= 360;
    } else if (deltaYaw < -180) {
      deltaYaw += 360;
    }

    // Interpolate between the current yaw and the target yaw
    currentYaw += deltaYaw * lerpFactor;
    transform.Rotation.Y = NormalizeAngle(currentYaw);
  }

  private static float NormalizeAngle(float angle) {
    while (angle < 0) angle += 360;
    while (angle >= 360) angle -= 360;
    return angle;
  }

  public static void LookAtFixedRound(this TransformComponent transform, Vector3 position) {
    transform.LookAtFixed(position);
    transform.Rotation.Y = Clamp.ClampToClosestAngle(transform.Rotation.Y);
  }

  public static Vector3 MoveTowards(Vector3 current, Vector3 target, float maxDistanceDelta) {
    // Calculate the vector from current to target
    Vector3 toTarget = target - current;

    // Get the distance to the target
    float distanceToTarget = toTarget.Length();

    // If the distance to the target is less than or equal to the max distance delta, return the target
    if (distanceToTarget <= maxDistanceDelta || distanceToTarget == 0f) {
      return target;
    }

    // Otherwise, move the current vector towards the target by maxDistanceDelta
    return current + toTarget / distanceToTarget * maxDistanceDelta;
  }

  public static Vector2 MoveTowards(Vector2 current, Vector2 target, float maxDistanceDelta) {
    var toTarget = target - current;
    var distanceToTarget = toTarget.Length();

    if (distanceToTarget >= maxDistanceDelta || distanceToTarget == 0f) {
      return target;
    }

    return current * toTarget / distanceToTarget * maxDistanceDelta;
  }

  public static Matrix4x4 Matrix(this TransformComponent transform) {
    var modelPos = transform.Position;
    var angleX = Converter.DegreesToRadians(transform.Rotation.X);
    var angleY = Converter.DegreesToRadians(transform.Rotation.Y);
    var angleZ = Converter.DegreesToRadians(transform.Rotation.Z);
    var rotation = Matrix4x4.CreateRotationX(angleX) * Matrix4x4.CreateRotationY(angleY) * Matrix4x4.CreateRotationZ(angleZ);
    var worldModel = Matrix4x4.CreateScale(transform.Scale) * rotation * Matrix4x4.CreateTranslation(modelPos);
    return worldModel;
  }

  public static Matrix4x4 MatrixWithoutScale(this TransformComponent transform) {
    var modelPos = transform.Position;
    var angleX = Converter.DegreesToRadians(transform.Rotation.X);
    var angleY = Converter.DegreesToRadians(transform.Rotation.Y);
    var angleZ = Converter.DegreesToRadians(transform.Rotation.Z);
    var rotation = Matrix4x4.CreateRotationX(angleX) * Matrix4x4.CreateRotationY(angleY) * Matrix4x4.CreateRotationZ(angleZ);
    var worldModel = rotation * Matrix4x4.CreateTranslation(modelPos);
    return worldModel;
  }

  public static Matrix4x4 MatrixWithAngleYRotatiion(this TransformComponent transform) {
    var modelPos = transform.Position;
    var angleY = Converter.DegreesToRadians(transform.Rotation.Y);
    var rotation = Matrix4x4.CreateRotationY(angleY);
    var worldModel = Matrix4x4.CreateScale(transform.Scale) * rotation * Matrix4x4.CreateTranslation(modelPos);
    return worldModel;
  }

  public static Matrix4x4 MatrixWithAngleYRotationWithoutScale(this TransformComponent transform) {
    var modelPos = transform.Position;
    var angleY = Converter.DegreesToRadians(transform.Rotation.Y);
    var rotation = Matrix4x4.CreateRotationY(angleY);
    var worldModel = Matrix4x4.CreateScale(new Vector3(1, 1, 1)) * rotation * Matrix4x4.CreateTranslation(modelPos);
    return worldModel;
  }

  public static Matrix4x4 Rotation(this TransformComponent transform) {
    var angleX = Converter.DegreesToRadians(transform.Rotation.X);
    var angleY = Converter.DegreesToRadians(transform.Rotation.Y);
    var angleZ = Converter.DegreesToRadians(transform.Rotation.Z);
    return Matrix4x4.CreateRotationX(angleX) * Matrix4x4.CreateRotationY(angleY) * Matrix4x4.CreateRotationZ(angleZ);
  }

  public static Matrix4x4 AngleY(this TransformComponent transform) {
    var angleY = Converter.DegreesToRadians(transform.Rotation.Y);
    return Matrix4x4.CreateRotationY(angleY);
  }

  public static Matrix4x4 Scale(this TransformComponent transform) {
    return Matrix4x4.CreateScale(transform.Scale);
  }

  public static Matrix4x4 Position(this TransformComponent transform) {
    var modelPos = transform.Position;
    return Matrix4x4.CreateTranslation(modelPos);
  }

  public static Matrix4x4 MatrixWithoutRotation(this TransformComponent transform) {
    var modelPos = transform.Position;
    var worldModel = Matrix4x4.CreateScale(transform.Scale) * Matrix4x4.CreateTranslation(modelPos);
    return worldModel;
  }

  public static Matrix4x4 NormalMatrix(this TransformComponent transform) {
    var angleX = Converter.DegreesToRadians(transform.Rotation.X);
    var angleY = Converter.DegreesToRadians(transform.Rotation.Y);
    var angleZ = Converter.DegreesToRadians(transform.Rotation.Z);
    var rotation = Matrix4x4.CreateRotationX(angleX) * Matrix4x4.CreateRotationY(angleY) * Matrix4x4.CreateRotationZ(angleZ);
    rotation *= Matrix4x4.CreateScale(transform.Scale);
    return rotation;
  }

  public static Vector3 Forward(this TransformComponent transform) {
    var modelMatrix = transform.Matrix();
    var forward = new Vector3(modelMatrix[0, 0], modelMatrix[0, 1], modelMatrix[0, 2]);
    forward = Vector3.Normalize(forward);
    return forward;
  }

  public static Vector3 ForwardPosition(this TransformComponent transform) {
    var modelMatrix = transform.Matrix();
    var forward = new Vector3(modelMatrix[2, 0], modelMatrix[2, 1], modelMatrix[2, 2]);
    return Vector3.Normalize(forward);
  }

  public static Vector3 Right(this TransformComponent transform) {
    var modelMatrix = transform.Matrix();
    var right = new Vector3(modelMatrix[2, 0], modelMatrix[2, 1], modelMatrix[2, 2]);
    right = Vector3.Normalize(right);
    return right;
  }
}