using System.Numerics;
using Neko.EntityComponentSystem;
using Neko.Globals;
using Neko.Windowing;
// using Neko.Extensions.GLFW;
// using static Neko.Extensions.GLFW.GLFW;

namespace Neko;

public class FreeCameraController : NekoScript {
  private TransformComponent _transform = null!;
  private Camera _camera = null!;

  public override void Start() {
    _transform = Owner.GetTransform()!;
    _camera = Owner.GetCamera()!;

    base.Start();
  }

  public override void Update() {
    var useController = 0;
    if (useController == 1) {

    } else {
      MoveByPC();
      LightHandler();
    }
  }
  public unsafe void MoveByPC() {
    if (CameraState.GetFirstMove()) {
      CameraState.SetFirstMove(false);
      CameraState.SetLastPosition(Input.MousePosition);
    } else {
      var deltaX = (float)Input.MousePosition.X - (float)CameraState.GetLastPosition().X;
      var deltaY = (float)Input.MousePosition.Y - (float)CameraState.GetLastPosition().Y;
      CameraState.SetLastPosition(Input.MousePosition);

      if (Window.MouseCursorState == CursorState.Centered) {
        _camera.Yaw += deltaX * CameraState.GetSensitivity();
        _camera.Pitch += deltaY * CameraState.GetSensitivity();
      }

      if (Input.GetKey(Scancode.D)) {
        _transform.Position += _camera.Right * CameraState.GetCameraSpeed() * Time.DeltaTime;
      }
      if (Input.GetKey(Scancode.A)) {
        _transform.Position -= _camera.Right * CameraState.GetCameraSpeed() * Time.DeltaTime;
      }
      if (Input.GetKey(Scancode.S)) {
        _transform.Position -= _camera.Front * CameraState.GetCameraSpeed() * Time.DeltaTime;
      }
      if (Input.GetKey(Scancode.W)) {
        _transform.Position += _camera.Front * CameraState.GetCameraSpeed() * Time.DeltaTime;
      }
      if (Input.GetKey(Scancode.Space)) {
        _transform.Position -= _camera.Up * CameraState.GetCameraSpeed() * Time.DeltaTime;
      }
      if (Input.GetKey(Scancode.LeftShift)) {
        _transform.Position += _camera.Up * CameraState.GetCameraSpeed() * Time.DeltaTime;
      }

      //if (glfwGetKey(_window.GLFWwindow, (int)GLFWKeyMap.Keys.GLFW_KEY_F) == (int)GLFWKeyMap.KeyAction.GLFW_PRESS) {
      //WindowState.FocusOnWindow();
      //}
    }
  }

  public void LightHandler() {
    // if (Input.GetKeyDown(Keycode.L)) {
    //   var app = Application.Instance;
    //   var light = new Entity() {
    //     Name = "pointlight"
    //   };
    //   light.AddTransform(Owner.GetComponent<Transform>().Position);
    //   light.AddComponent(new PointLightComponent());
    //   light.GetComponent<PointLightComponent>().Color = new Vector4(
    //     1,
    //     1f,
    //     1f,
    //     0.4f
    //   );
    //   app.AddEntity(light);
    // }
  }
}