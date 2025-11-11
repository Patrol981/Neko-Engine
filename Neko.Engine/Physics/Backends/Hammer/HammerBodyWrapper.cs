using System.Numerics;
using DotTiled;
using Neko.EntityComponentSystem;
using Neko.Extensions.Logging;
using Neko.Hammer;
using Neko.Hammer.Models;
using Neko.Physics.Interfaces;
using Neko.Rendering;
using Neko.Rendering.Renderer2D.Components;
using Neko.Rendering.Renderer2D.Helpers;
using Neko.Rendering.Renderer2D.Interfaces;
using Neko.Rendering.Renderer3D;
using ZLinq;

namespace Neko.Physics.Backends.Hammer;

public class HammerBodyWrapper : IPhysicsBody2D {
  private readonly HammerInterface _hammerInterface;
  private BodyId _bodyId = null!;

  public HammerBodyWrapper(in HammerInterface hammerInterface) {
    _hammerInterface = hammerInterface;
  }

  public object BodyId => _bodyId;
  public bool Grounded => _hammerInterface.GetGrounded(_bodyId);

  public Vector2 Position {
    get => _hammerInterface.GetPosition(_bodyId);
    set => _hammerInterface.SetPosition(_bodyId, value);
  }
  public Vector2 LinearVelocity {
    get => _hammerInterface.GetVelocity(_bodyId);
    set => _hammerInterface.SetVelocity(_bodyId, value);
  }
  public Vector2 AngularVelocity {
    get => _hammerInterface.GetVelocity(_bodyId);
    set => _hammerInterface.SetVelocity(_bodyId, value);
  }
  public float GravityFactor {
    get => _hammerInterface.GetGravity();
    set => _hammerInterface.SetGravity(value);
  }
  public MotionQuality MotionQuality {
    get => (MotionQuality)_hammerInterface.GetMotionQuality(_bodyId);
    set => _hammerInterface.SetMotionQuality(_bodyId, (Neko.Hammer.Enums.MotionQuality)value);
  }
  public MotionType MotionType {
    get => (MotionType)_hammerInterface.GetMotionType(_bodyId);
    set => _hammerInterface.SetMotionType(_bodyId, (Neko.Hammer.Enums.MotionType)value);
  }

  public object CreateAndAddBody(object settings) {
    _bodyId = _hammerInterface.CreateAndAddBody((ShapeSettings)settings, Neko.Hammer.Enums.MotionType.Dynamic, Vector2.Zero, false);

    return null!;
  }

  public object ColldierMeshToPhysicsShape(Entity entity, Mesh colliderMesh) {
    var transform = entity.GetTransform();
    List<Neko.Hammer.Structs.Vertex> vertices = [];
    foreach (var m in colliderMesh.Vertices) {
      Neko.Hammer.Structs.Vertex v = new() {
        X = m.Position.X,
        Y = m.Position.Y
      };

      vertices.Add(v);
    }

    var rigidbody = entity.GetRigidbody2D();

    object userData;
    Neko.Hammer.Enums.ObjectType objectType = Neko.Hammer.Enums.ObjectType.Sprite;

    var drawable = entity.GetDrawable2D();
    if (entity.HasAndImplementComponent<Tilemap, IDrawable2D>(drawable)) {
      // var edges = tilemap.ExtractEgdges();
      // var hammerEdges = new List<Neko.Hammer.Structs.Edge>();
      // foreach (var edge in edges) {
      //   hammerEdges.Add(new() {
      //     A = edge.A,
      //     B = edge.B,
      //     Normal = edge.Normal,
      //   });
      // }

      var tilemap = drawable as Tilemap;
      var aabbs = tilemap!.ExtractAABBs();

      userData = aabbs;
      objectType = Neko.Hammer.Enums.ObjectType.Tilemap;
    } else {
      userData = (rigidbody?.Min, rigidbody?.Max);
    }

    ShapeSettings shapeSettings = new ShapeSettings(
      new Neko.Hammer.Structs.Mesh() {
        Vertices = [.. vertices],
        Indices = colliderMesh.Indices
      },
      userData,
      objectType
    );

    return shapeSettings;
  }

  public void CreateAndAddBody(MotionType motionType, object shapeSettings, Vector2 position, bool isTrigger) {
    _bodyId = _hammerInterface.CreateAndAddBody(
      (ShapeSettings)shapeSettings,
      (Neko.Hammer.Enums.MotionType)motionType,
      position,
      isTrigger
    );
  }

  public void RemoveBody() {
    _hammerInterface.RemoveBody((Neko.Hammer.Models.BodyId)_bodyId);
  }

  public void SetActive(bool value) {
    throw new NotImplementedException();
  }

  public void AddForce(Vector2 force) {
    _hammerInterface.AddForce(_bodyId, force);
  }

  public void AddLinearVelocity(Vector2 velocity) {
    _hammerInterface.AddVelocity(_bodyId, velocity);
  }

  public void AddImpulse(Vector2 impulse) {
    throw new NotImplementedException();
  }

  public static (Rigidbody2D?, Rigidbody2D?) GetCollisionData(BodyId body1, BodyId body2) {
    var entities = Application.Instance.Entities.Where(x => !x.CanBeDisposed && x.HasComponent<Rigidbody2D>());
    var first = entities.Where(x => (BodyId)x.GetRigidbody2D()!.PhysicsBody2D.BodyId == body1).FirstOrDefault()?.GetRigidbody2D();
    var second = entities.Where(x => (BodyId)x.GetRigidbody2D()!.PhysicsBody2D.BodyId == body2).FirstOrDefault()?.GetRigidbody2D();

    return (first, second);
  }

  public static Rigidbody2D? GetCollisionData(BodyId body1) {
    foreach (var e in Application.Instance.Entities) {
      if (e.CanBeDisposed) continue;
      if (!e.HasComponent<Rigidbody2D>()) continue;

      var rb = e.GetRigidbody2D();
      if ((BodyId)rb!.PhysicsBody2D.BodyId == body1) {
        return rb;
      }
    }

    return null;

    // var entity = Application.Instance.Entities
    //   .Where(x => !x.CanBeDisposed && x.HasComponent<Rigidbody2D>())
    //   .Where(x => (BodyId)x.GetRigidbody2D()!.PhysicsBody2D.BodyId == body1)
    //   .SingleOrDefault()?
    //   .GetRigidbody2D();

    // return entity;
  }

  public void Dispose() {
    RemoveBody();
  }
}