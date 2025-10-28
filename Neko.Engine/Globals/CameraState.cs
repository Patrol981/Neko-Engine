using Neko.EntityComponentSystem;
using System.Numerics;
namespace Neko.Globals;

public static class CameraState {
  private static Camera? _camera;
  private static Entity? _cameraEntity;
  private static float _cameraSpeed = 3.0f;
  private static float _sensitivity = 0.2f;
  private static bool _firstMove = true;
  private static Vector2 _lastPos;

  public static void SetCamera(Camera camera) {
    _camera = camera;
  }

  public static void SetCameraEntity(Entity cameraEntity) {
    _cameraEntity = cameraEntity;
  }

  public static void SetCameraSpeed(float cameraSpeed) {
    _cameraSpeed = cameraSpeed;
  }

  public static void SetSensitivity(float sensitivity) {
    _sensitivity = sensitivity;
  }

  public static void SetFirstMove(bool firstMove) {
    _firstMove = firstMove;
  }
  public static void SetLastPosition(Vector2 lastPos) {
    _lastPos = lastPos;
  }

  public static Camera GetCamera() {
    if (_camera == null) {
      _camera = new Camera(null!, float.NaN, float.NaN);
    }
    return _camera;
  }

  public static Entity GetCameraEntity() {
    if (_cameraEntity == null) {
      _cameraEntity = new Entity("camera");
    }
    return _cameraEntity;
  }

  public static float GetCameraSpeed() {
    return _cameraSpeed;
  }

  public static float GetSensitivity() {
    return _sensitivity;
  }

  public static bool GetFirstMove() {
    return _firstMove;
  }
  public static Vector2 GetLastPosition() {
    return _lastPos;
  }
}