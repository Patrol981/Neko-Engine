using System.Numerics;

using Dwarf.EntityComponentSystem;
using Dwarf.Math;
using Dwarf.Rendering.Renderer2D.Components;

namespace Dwarf.EntityComponentSystemLegacy;

public static class TransformExtensions {
  public static Matrix4x4 OverrideZDepth(this Matrix4x4 mat4, float z) {
    return Matrix4x4.CreateTranslation(0, 0, z) * mat4;
  }
}

public class Transform : Component {
  public Vector3 Position;
  public Vector3 Rotation;
  public Vector3 Scale;

  public Transform() {
    Position = new Vector3(0, 0, 0);
    Rotation = new Vector3(0, 0, 0);
    Scale = new Vector3(1, 1, 1);
  }

  public Transform(Vector3 position) {
    Position = position != Vector3.Zero ? position : new Vector3(0, 0, 0);
    Rotation = new Vector3(0, 0, 0);
    Scale = new Vector3(1, 1, 1);
  }

  public Transform(Vector3 position, Vector3 rotation) {
    Position = position != Vector3.Zero ? position : new Vector3(0, 0, 0);
    Rotation = rotation != Vector3.Zero ? rotation : new Vector3(0, 0, 0);
    Scale = new Vector3(1, 1, 1);
  }

  public Transform(Vector3 position, Vector3 rotation, Vector3 scale) {
    Position = position != Vector3.Zero ? position : new Vector3(0, 0, 0);
    Rotation = rotation != Vector3.Zero ? rotation : new Vector3(0, 0, 0);
    Scale = scale != Vector3.Zero ? scale : new Vector3(1, 1, 1);
  }

  public void IncreasePosition(Vector3 position) {
    Position.X += position.X;
    Position.Y += position.Y;
    Position.Z += position.Z;
  }

  public void IncreaseRotation(Vector3 rotation) {
    Rotation.X += rotation.X;
    Rotation.Y += rotation.Y;
    Rotation.Z += rotation.Z;

    if (Rotation.X > 360) {
      var offset = Rotation.X - 360;
      Rotation.X = 0 + offset;
    }

    if (Rotation.Y > 360) {
      var offset = Rotation.Y - 360;
      Rotation.Y = 0 + offset;
    }

    if (Rotation.Z > 360) {
      var offset = Rotation.Z - 360;
      Rotation.Z = 0 + offset;
    }
  }

  public void IncreaseRotationX(float value) {
    Rotation.X += value;

    if (Rotation.X > 360) {
      var offset = Rotation.X - 360;
      Rotation.X = 0 + offset;
    }
  }

  public void IncreaseRotationY(float value) {
    Rotation.Y += value;

    if (Rotation.Y > 360) {
      var offset = Rotation.Y - 360;
      Rotation.Y = 0 + offset;
    }
  }

  public void IncreaseRotationZ(float value) {
    Rotation.Z += value;

    if (Rotation.Z > 360) {
      var offset = Rotation.Z - 360;
      Rotation.Z = 0 + offset;
    }
  }

  /// <summary>
  /// Sets Transform euler angle to given position
  /// </summary>
  /// <param name="position"></param>
  public void LookAt(Vector3 position) {
    var direction = position - Position;
    direction = Vector3.Normalize(direction);
    var yaw = MathF.Atan2(-direction.X, -direction.Z);
    yaw = Converter.RadiansToDegrees(yaw);
    var pitch = MathF.Asin(direction.Y);
    pitch = -Converter.RadiansToDegrees(pitch);
    Rotation.Y = yaw;
    Rotation.X = pitch;
  }

  /// <summary>
  /// Sets Transform euler angle to given position
  /// </summary>
  /// <param name="position"></param>
  public static Vector3 LookAt(Transform transform, Vector3 position) {
    var rotation = Vector3.Zero;
    var direction = position - transform.Position;
    direction = Vector3.Normalize(direction);
    var yaw = MathF.Atan2(-direction.X, -direction.Z);
    yaw = Converter.RadiansToDegrees(yaw);
    var pitch = MathF.Asin(direction.Y);
    pitch = -Converter.RadiansToDegrees(pitch);
    rotation.Y = yaw;
    rotation.X = pitch;
    return rotation;
  }

  /// <summary>
  /// Sets Transform euler angle to given position only by Y axis
  /// </summary>
  /// <param name="position"></param>
  public void LookAtFixed(Vector3 position) {
    var direction = position - Position;
    direction.Y = 0;
    direction = Vector3.Normalize(direction);
    var yaw = MathF.Atan2(-direction.X, -direction.Z);
    yaw = Converter.RadiansToDegrees(yaw);
    Rotation.Y = yaw;
  }

  /// <summary>
  /// Sets Transform euler angle to given position only by Z axis
  /// </summary>
  /// <param name="position"></param>
  public void LookAtFixed2D(Vector2 position) {
    var direction = position - Position.ToVector2();
    direction = Vector2.Normalize(direction);
    var angle = MathF.Atan2(direction.X, direction.Y);
    angle = Converter.RadiansToDegrees(angle);
    Rotation.Z = angle;
  }

  /// <summary>
  /// Sets Transform euler angle to given position only by Y axis
  /// </summary>
  /// <param name="position"></param>
  public void LookAtFixed(Vector3 position, float lerpFactor) {
    // Calculate the target direction
    var direction = position - Position;
    direction.Y = 0;
    direction = Vector3.Normalize(direction);

    // Calculate the target yaw angle
    var targetYaw = MathF.Atan2(-direction.X, -direction.Z);
    targetYaw = Converter.RadiansToDegrees(targetYaw);

    // Normalize both angles to the range [0, 360)
    var currentYaw = NormalizeAngle(Rotation.Y);
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
    Rotation.Y = NormalizeAngle(currentYaw);
  }

  private static float NormalizeAngle(float angle) {
    while (angle < 0) angle += 360;
    while (angle >= 360) angle -= 360;
    return angle;
  }

  public void LookAtFixedRound(Vector3 position) {
    LookAtFixed(position);
    Rotation.Y = Clamp.ClampToClosestAngle(Rotation.Y);
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

  [Obsolete]
  public static Vector3 MoveTowards_Old(Vector3 current, Vector3 target, float maxDistanceDelta) {
    var vec = target - current;
    var mag = Vector3.Distance(current, target);

    if (mag <= maxDistanceDelta || mag == 0.0f) {
      return target;
    }
    return current + Vector3.Normalize(vec) * maxDistanceDelta;
  }

  private Matrix4x4 GetMatrix() {
    var modelPos = Position;
    var angleX = Converter.DegreesToRadians(Rotation.X);
    var angleY = Converter.DegreesToRadians(Rotation.Y);
    var angleZ = Converter.DegreesToRadians(Rotation.Z);
    var rotation = Matrix4x4.CreateRotationX(angleX) * Matrix4x4.CreateRotationY(angleY) * Matrix4x4.CreateRotationZ(angleZ);
    var worldModel = Matrix4x4.CreateScale(Scale) * rotation * Matrix4x4.CreateTranslation(modelPos);
    return worldModel;
  }

  private Matrix4x4 GetMatrixWithoutScale() {
    var modelPos = Position;
    var angleX = Converter.DegreesToRadians(Rotation.X);
    var angleY = Converter.DegreesToRadians(Rotation.Y);
    var angleZ = Converter.DegreesToRadians(Rotation.Z);
    var rotation = Matrix4x4.CreateRotationX(angleX) * Matrix4x4.CreateRotationY(angleY) * Matrix4x4.CreateRotationZ(angleZ);
    var worldModel = rotation * Matrix4x4.CreateTranslation(modelPos);
    return worldModel;
  }

  private Matrix4x4 GetMatrixWithYAngleRotation() {
    var modelPos = Position;
    var angleY = Converter.DegreesToRadians(Rotation.Y);
    var rotation = Matrix4x4.CreateRotationY(angleY);
    var worldModel = Matrix4x4.CreateScale(Scale) * rotation * Matrix4x4.CreateTranslation(modelPos);
    return worldModel;
  }

  private Matrix4x4 GetMatrixWithAngleYRotationWithoutScale() {
    var modelPos = Position;
    var angleY = Converter.DegreesToRadians(Rotation.Y);
    var rotation = Matrix4x4.CreateRotationY(angleY);
    var worldModel = Matrix4x4.CreateScale(new Vector3(1, 1, 1)) * rotation * Matrix4x4.CreateTranslation(modelPos);
    return worldModel;
  }

  private Matrix4x4 GetRotation() {
    var angleX = Converter.DegreesToRadians(Rotation.X);
    var angleY = Converter.DegreesToRadians(Rotation.Y);
    var angleZ = Converter.DegreesToRadians(Rotation.Z);
    return Matrix4x4.CreateRotationX(angleX) * Matrix4x4.CreateRotationY(angleY) * Matrix4x4.CreateRotationZ(angleZ);
  }

  private Matrix4x4 GetAngleY() {
    var angleY = Converter.DegreesToRadians(Rotation.Y);
    return Matrix4x4.CreateRotationY(angleY);
  }

  private Matrix4x4 GetScale() {
    return Matrix4x4.CreateScale(Scale);
  }

  private Matrix4x4 GetPosition() {
    var modelPos = Position;
    return Matrix4x4.CreateTranslation(modelPos);
  }

  private Matrix4x4 GetMatrixWithoutRotation() {
    var modelPos = Position;
    var worldModel = Matrix4x4.CreateScale(Scale) * Matrix4x4.CreateTranslation(modelPos);
    return worldModel;
  }

  private Matrix4x4 GetNormalMatrix() {
    var angleX = Converter.DegreesToRadians(Rotation.X);
    var angleY = Converter.DegreesToRadians(Rotation.Y);
    var angleZ = Converter.DegreesToRadians(Rotation.Z);
    var rotation = Matrix4x4.CreateRotationX(angleX) * Matrix4x4.CreateRotationY(angleY) * Matrix4x4.CreateRotationZ(angleZ);
    rotation *= Matrix4x4.CreateScale(Scale);
    return rotation;
  }

  private Vector3 GetForward() {
    var modelMatrix = Matrix4;
    var forward = new Vector3(modelMatrix[0, 0], modelMatrix[0, 1], modelMatrix[0, 2]);
    forward = Vector3.Normalize(forward);
    return forward;
  }

  private Vector3 GetForwardPosition() {
    var modelMatrix = Matrix4;
    var forward = new Vector3(modelMatrix[2, 0], modelMatrix[2, 1], modelMatrix[2, 2]);
    return Vector3.Normalize(forward);
  }

  private Vector3 GetRight() {
    var modelMatrix = Matrix4;
    var right = new Vector3(modelMatrix[2, 0], modelMatrix[2, 1], modelMatrix[2, 2]);
    right = Vector3.Normalize(right);
    return right;
  }

  public Vector2 GetClipSpace(FrameInfo frameInfo) {
    var model = Matrix4x4.CreateScale(Scale) * Matrix4x4.CreateTranslation(Position);
    var mvp = model * frameInfo.Camera.GetViewMatrix() * frameInfo.Camera.GetProjectionMatrix();
    // var pvm = frameInfo.Camera.GetProjectionMatrix() * frameInfo.Camera.GetViewMatrix() * modelMatrix;
    // var clip = mvp * new Vector4(Position, 1.0f);
    var clip = Vector4.Transform(new Vector4(Position, 1.0f), mvp);
    var ndc = new Vector3(clip.X, clip.Y, clip.Z) / clip.W;
    var screenPos = new Vector2(ndc.X * 0.5f + 0.5f, ndc.Y * 0.5f + 0.5f);

    return Vector2.Normalize(new Vector2(clip.X, clip.Z));
  }

  public Vector3 Forward => GetForward();
  public Vector3 ForwardPosition => GetForwardPosition();
  public Vector3 Right => GetRight();
  public Matrix4x4 Matrix4 => GetMatrix();
  public Matrix4x4 NoScale => GetMatrixWithoutScale();
  public Matrix4x4 ScaleMatrix => GetScale();
  public Matrix4x4 RotationMatrix => GetRotation();
  public Matrix4x4 AngleYMatrix => GetAngleY();
  public Matrix4x4 PositionMatrix => GetPosition();
  public Matrix4x4 MatrixWithoutRotation => GetMatrixWithoutRotation();
  public Matrix4x4 MatrixWithAngleYRotation => GetMatrixWithYAngleRotation();
  public Matrix4x4 MatrixWithAngleYRotationWithoutScale => GetMatrixWithAngleYRotationWithoutScale();
  public Matrix4x4 NormalMatrix => GetNormalMatrix();

  /*
  public Vector3 Position {
    get { return Position; }
    set { Position = value; }
  }
  public Vector3 Rotation {
    get { return Rotation; }
    set { Rotation = value; }
  }
  public Vector3 Scale {
    get { return Scale; }
    set { Scale = value; }
  }
  */

}