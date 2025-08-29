using System.Numerics;

using Dwarf.EntityComponentSystem;
using Dwarf.Math;

namespace Dwarf;

public enum CameraType {
  None,
  Perspective,
  Orthographic
}

public class Camera {
  private Matrix4x4 _projectionMatrix = Matrix4x4.Identity;
  private Matrix4x4 _viewMatrix = Matrix4x4.Identity;

  protected Vector3 _front = -Vector3.UnitZ;
  protected Vector3 _forward = -Vector3.UnitZ;
  protected Vector3 _up = -Vector3.UnitY;
  protected Vector3 _right = Vector3.UnitX;

  protected CameraType _cameraType = CameraType.None;

  internal float _pitch = 0f;
  internal float _yaw = -MathF.PI; // Without this, you would be started rotated 90 degrees right.
  internal float _fov = -MathF.PI;
  internal float _aspect = 1;

  internal Entity Owner { get; init; }

  public Camera(Entity owner) {
    Owner = owner;
  }

  public Camera(Entity owner, float fov, float aspect) {
    _fov = fov;
    _aspect = aspect;
    Owner = owner;
  }

  public void SetOrthograpicProjection() {
    SetOrthograpicProjection(-_aspect, _aspect, -1, 1, 0.1f, 100f);
    _cameraType = CameraType.Orthographic;
  }

  public void SetOrthograpicProjection(float near, float far) {
    SetOrthograpicProjection(-_aspect, _aspect, -1, 1, near, far);
    _cameraType = CameraType.Orthographic;
  }

  public void SetOrthograpicProjection(float left, float right, float top, float bottom, float near, float far) {
    _projectionMatrix = Matrix4x4.Identity;
    _projectionMatrix[0, 0] = 2.0f / (right - left);
    _projectionMatrix[1, 1] = 2.0f / (bottom - top);
    _projectionMatrix[2, 2] = 1.0f / (far - near);
    _projectionMatrix[0, 3] = -(right + left) / (right - left);
    _projectionMatrix[1, 3] = -(bottom + top) / (bottom - top);
    _projectionMatrix[2, 3] = -near / (far - near);
    _cameraType = CameraType.Orthographic;

    Near = near;
    Far = far;
  }

  public void SetPerspectiveProjection(float near, float far) {
    _projectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(
      Converter.DegreesToRadians(_fov),
      _aspect,
      near,
      far
    );

    Near = near;
    Far = far;

    _cameraType = CameraType.Perspective;
  }

  public Matrix4x4 GetProjectionMatrix() {
    return _projectionMatrix;
  }

  public Matrix4x4 GetProjectionMatrix2D() {
    float tanHalfFovy = (float)MathF.Tan(_fov / 2.0f);
    var projectionMatrix = new Matrix4x4();
    projectionMatrix[0, 0] = 1.0f / (_aspect * tanHalfFovy);
    projectionMatrix[1, 1] = 1.0f / (tanHalfFovy);
    projectionMatrix[2, 2] = 100.0f / (100.0f - 0.0f);
    projectionMatrix[2, 3] = 1.0f;
    projectionMatrix[3, 2] = -(100.0f * 0.0f) / (100.0f - 0.0f);
    return projectionMatrix;
  }

  public Matrix4x4 GetViewMatrix() {
    Vector3 position = Owner.GetTransform()!.Position;
    _viewMatrix = Matrix4x4.CreateLookAt(position, position + _front, _up);
    return _viewMatrix;
  }

  public float Pitch {
    get => Converter.RadiansToDegrees(_pitch);
    set {
      var angle = System.Math.Clamp(value, -89f, 89f);
      _pitch = Converter.DegreesToRadians(angle);
      UpdateVectors();
    }
  }

  public float Yaw {
    get => Converter.RadiansToDegrees(_yaw);
    set {
      _yaw = Converter.DegreesToRadians(value);
      UpdateVectors();
    }
  }
  public float Fov {
    get => Converter.RadiansToDegrees(_fov);
    set {
      var angle = System.Math.Clamp(value, 1f, 45f);
      _fov = Converter.DegreesToRadians(angle);
    }
  }

  public float RawFov => _fov;

  public void UpdateVectors() {
    _front.X = MathF.Cos(_pitch) * MathF.Cos(_yaw);
    _front.Y = MathF.Sin(_pitch);
    _front.Z = MathF.Cos(_pitch) * MathF.Sin(_yaw);

    _forward.X = MathF.Cos(_yaw);
    _forward.Z = MathF.Sin(_yaw);

    _forward = Vector3.Normalize(_forward);
    _front = Vector3.Normalize(_front);

    _right = Vector3.Normalize(Vector3.Cross(_front, Vector3.UnitY));
    _up = Vector3.Normalize(Vector3.Cross(_right, _front));
  }

  public float Aspect {
    get { return _aspect; }
    set { _aspect = value; }
  }

  public Vector3 Front => _front;
  public Vector3 Forward => _forward;
  public Vector3 Right => _right;
  public Vector3 Up => _up;
  public CameraType CameraType => _cameraType;

  public float Far { get; private set; }
  public float Near { get; private set; }
}