using Neko.EntityComponentSystem;

using Box2D.NET;

using static Box2D.NET.B2Joints;
using static Box2D.NET.B2Geometries;
using static Box2D.NET.B2Types;
using static Box2D.NET.B2MathFunction;
using static Box2D.NET.B2Bodies;
using static Box2D.NET.B2Shapes;
using static Box2D.NET.B2Worlds;
using static Box2D.NET.B2BodyDef;
using Neko.Extensions.Logging;

namespace Neko.Physics.Backends.Box2D;

public sealed class Box2DProgram : IPhysicsProgram {
  public Dictionary<Entity, Box2DBodyWrapper> Bodies { get; private set; } = [];
  public const float DeltaTime = 1.0f / 60.0f;

  /// <summary>
  /// The suggested sub-step count for Box2D is 4. 
  /// Keep in mind that this has a trade-off between performance and accuracy. 
  /// Using fewer sub-steps increases performance but accuracy suffers. 
  /// Likewise, using more sub-steps decreases performance but improves the quality of your simulation. 
  /// </summary>
  public const int SubstepCount = 4;

  private B2World _world;
  private B2WorldId _worldId;

  private B2BodyId _groundBodyId;

  public Box2DProgram(bool createGround = true) {
    var def = b2DefaultWorldDef();
    def.gravity = new(0, -0.000000000010f);

    _worldId = b2CreateWorld(ref def);
    _world = b2GetWorldFromId(_worldId);

    if (createGround) {
      var groundBodyDef = b2DefaultBodyDef();
      groundBodyDef.position = new(0, 5);

      _groundBodyId = b2CreateBody(_worldId, ref groundBodyDef);
      var groundPolygon = b2MakeBox(50, 0.5f);

      var groundShapeDef = b2DefaultShapeDef();
      b2CreatePolygonShape(_groundBodyId, ref groundShapeDef, ref groundPolygon);
    }
  }

  public void Init(Span<Entity> entities) {
    foreach (var entity in entities) {
      var wrapper = new Box2DBodyWrapper(_world, _worldId);
      Bodies.Add(entity, wrapper);
      entity.GetRigidbody2D()?.Init(wrapper);
    }
  }

  public void Update() {
    b2World_Step(_worldId, DeltaTime, SubstepCount);
    var worldEvents = b2World_GetContactEvents(_worldId);
    foreach (var body in Bodies.Values) {
      body.Update();
    }
    if (worldEvents.hitCount != 0)
      Logger.Info($"{worldEvents.hitCount}");
  }

  public void Dispose() {
    // foreach (var body in Bodies) {
    //   if (body.Key.Collected) continue;
    //   body.Value.Dispose();
    //   // body.Key.GetRigidbody2D()?.Dispose();
    // }
    Bodies = [];
    b2DestroyBody(_groundBodyId);
    _world = default!;
    b2DestroyWorld(_worldId);
    _worldId = default;
    GC.SuppressFinalize(this);
  }
}