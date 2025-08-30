using Dwarf.EntityComponentSystem;
using Dwarf.Globals;
using Dwarf.Windowing;

namespace Dwarf;

public class TopDownCamera : DwarfScript {
  public override void Update() {
    MoveByPC();
  }
  public unsafe void MoveByPC() {
    if (Input.GetKey(Scancode.D)) {
      Owner.GetTransform()!.Position += Owner.GetCamera()!.Right * CameraState.GetCameraSpeed() * Time.DeltaTime;
    }
    if (Input.GetKey(Scancode.A)) {
      Owner.GetTransform()!.Position -= Owner.GetCamera()!.Right * CameraState.GetCameraSpeed() * Time.DeltaTime;
    }
    if (Input.GetKey(Scancode.W)) {
      Owner.GetTransform()!.Position -= Owner.GetCamera()!.Up * CameraState.GetCameraSpeed() * Time.DeltaTime;
    }
    if (Input.GetKey(Scancode.S)) {
      Owner.GetTransform()!.Position += Owner.GetCamera()!.Up * CameraState.GetCameraSpeed() * Time.DeltaTime;
    }

    if (Input.GetKey(Scancode.E)) {
      Owner.GetTransform()!.Position -= Owner.GetCamera()!.Front * CameraState.GetCameraSpeed() * Time.DeltaTime;
    }
    if (Input.GetKey(Scancode.Q)) {
      Owner.GetTransform()!.Position += Owner.GetCamera()!.Front * CameraState.GetCameraSpeed() * Time.DeltaTime;
    }
  }
}