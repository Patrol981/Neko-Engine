using System.Numerics;
using Neko.EntityComponentSystem;
using Neko.Globals;
using Neko.Rendering.Renderer3D;

namespace Neko;

public class DefaultScene : Scene {
  public DefaultScene(Application app) : base(app) { }

  public override void LoadEntities() {
    // var level = new Entity {
    //   Name = "level",
    // };
    // level.AddTransform(new(0, 0, 0), new(180, 0, 0), new(1f, 1f, 1f));
    // level.AddMaterial();
    // level.AddModel("./Resources/level01.glb", 0);
    // var levelNodes = level.GetComponent<MeshRenderer>().MeshedNodes;
    // foreach (var levelNode in levelNodes.Where(x => !x.Name.Contains("floor"))) {
    //   levelNode.FilterMeInShader = true;
    // }
    // AddEntity(level);

    // var camera = new Entity();
    // camera.AddComponent(new Transform(new Vector3(0, 0, 0)));
    // camera.AddComponent(new Camera(60, _app.Renderer.AspectRatio));
    // camera.GetComponent<Camera>()?.SetPerspectiveProjection(0.1f, 100f);
    // camera.GetComponent<Camera>().Yaw = -90.0f;
    // camera.AddComponent(new FreeCameraController());
    // CameraState.SetCamera(camera.GetComponent<Camera>());
    // CameraState.SetCameraEntity(camera);
    // _app.SetCamera(camera);
    // AddEntity(camera);
  }
}