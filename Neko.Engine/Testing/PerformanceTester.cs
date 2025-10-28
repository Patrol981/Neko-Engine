using Neko.EntityComponentSystem;
using Neko.Physics;
using SDL3;

namespace Neko.Testing;

public class PerformanceTester {
  public static void KeyHandler(SDL_Keycode key) {
    if (key == SDL_Keycode.P) CreateNewModel(Application.Instance, false);
    if (key == SDL_Keycode.LeftBracket) CreateNewModel(Application.Instance, true);
    if (key == SDL_Keycode.O) RemoveModel(Application.Instance);
  }

  public static Task CreateNewModel(Application app, bool addTexture = false) {
    // if (!addTexture) return Task.CompletedTask;

    var entity = new Entity("test");
    entity.AddTransform([-5, 0, 0], [90, 0, 0], [.25f]);
    entity.AddMaterial();
    // entity.AddPrimitive("./Resources/gigachad.png", PrimitiveType.Cylinder);
    entity.AddModel("./Resources/fox2.glb"); ;
    // entity.AddRigidbody(PrimitiveType.Cylinder, false, 1);
    // entity.GetComponent<Rigidbody>().Init(Application.Instance.Systems.PhysicsSystem.BodyInterface);
    app.AddEntity(entity);
    return Task.CompletedTask;
  }

  public static void RemoveModel(Application app) {
    var room = app.GetEntitiesEnumerable().Where(x => x.Name == "test").FirstOrDefault();
    if (room == null) return;
    room.CanBeDisposed = true;
  }
}
