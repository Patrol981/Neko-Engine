using System.Numerics;
using Neko.EntityComponentSystem;
using Neko.Physics.Interfaces;
using Neko.Rendering;
using Box2D.NET;

using static Box2D.NET.B2Joints;
using static Box2D.NET.B2Geometries;
using static Box2D.NET.B2Types;
using static Box2D.NET.B2MathFunction;
using static Box2D.NET.B2Bodies;
using static Box2D.NET.B2Shapes;
using static Box2D.NET.B2Worlds;
using static Box2D.NET.B2Hulls;
using Neko.Extensions.Logging;
using Neko.Rendering.Renderer2D.Interfaces;
using Neko.Rendering.Renderer2D.Components;
using Neko.Rendering.Renderer2D.Helpers;

namespace Neko.Physics.Backends.Box2D;

public sealed class Box2DBodyWrapper : IPhysicsBody2D {
  public Vector2 Position {
    get => b2Body_GetPosition(_bodyId).ToVec2;
    set {
      var rotation = b2Body_GetRotation(_bodyId);
      b2Body_SetTransform(_bodyId, value.FromVec2, rotation);
    }
  }
  public Vector2 LinearVelocity {
    get => b2Body_GetLinearVelocity(_bodyId).ToVec2;
    set => b2Body_SetLinearVelocity(_bodyId, value.FromVec2);
  }
  public Vector2 AngularVelocity {
    get => b2Body_GetAngularVelocity(_bodyId).ToVec2;
    set => b2Body_SetAngularVelocity(_bodyId, value.X);
  }
  public float GravityFactor {
    get => b2Body_GetGravityScale(_bodyId);
    set => b2Body_SetGravityScale(_bodyId, value);
  }
  public MotionQuality MotionQuality {
    get => MotionQuality.LinearCast;
    set { }
  }
  public MotionType MotionType {
    get => MotionType.Dynamic;
    set { }
  }

  public bool Grounded => false;
  public object BodyId => _bodyId;

  private B2World _world;
  private B2WorldId _worldId;
  private B2BodyId _bodyId;
  private B2Body _body = null!;
  private B2ShapeId _shapeId;
  private List<B2ShapeId> _shapeIndices = [];

  public Box2DBodyWrapper(B2World world, B2WorldId worldId) {
    _world = world;
    _worldId = worldId;
  }

  public void AddForce(Vector2 force) {
    B2Vec2 vec2 = new(force.X, force.Y);
    var pos = b2Body_GetPosition(_bodyId);
    b2Body_ApplyForce(_bodyId, vec2, pos, default);
  }

  public void AddImpulse(Vector2 impulse) {
    B2Vec2 vec2 = new(impulse.X, impulse.Y);
    var pos = b2Body_GetPosition(_bodyId);
    b2Body_ApplyLinearImpulse(_bodyId, vec2, pos, default);
  }

  public void AddLinearVelocity(Vector2 velocity) {
    // B2Vec2 vec2 = new(velocity.X, velocity.Y);
    // var vel = b2Body_GetLinearVelocity(_bodyId);
    // b2Body_SetLinearVelocity(_bodyId, vec2 + vel);

    // var pos = b2Body_GetPosition(_bodyId);
    // float angle = b2Body_GetAngle(_bodyId);
  }

  public object ColldierMeshToPhysicsShape(Entity entity, Mesh colliderMesh) {
    var b2Vertices = new B2Vec2[colliderMesh.VertexCount];
    for (int i = 0; i < colliderMesh.Vertices.Length; i++) {
      var pos = colliderMesh.Vertices[i].Position;
      b2Vertices[i] = new(pos.X, pos.Y);
    }

    var drawable = entity.GetDrawable2D();
    if (drawable?.DrawableType == Drawable2DType.Tilemap) {
      var tilemap = (drawable as Tilemap)!;
      var aabbs = tilemap.ExtractAABBs();

      var rects = tilemap.BuildCollisionRectangles();
      var polygons = new List<B2Polygon>(rects.Count);

      // int separator = 0;
      // foreach(var aabb in aabbs) {
      //   var hull = b2ComputeHull()
      // }

      foreach (var (center, halfExtents) in rects) {
        var poly = b2MakeBox(halfExtents.X, halfExtents.Y);

        for (int i = 0; i < poly.count; ++i) {
          poly.vertices[i].X += center.X;
          poly.vertices[i].Y += center.Y;
        }

        polygons.Add(poly);
      }

      return polygons;
    } else {
      var hull = b2ComputeHull(b2Vertices, b2Vertices.Length);
      if (hull.count < 1) {
        throw new ArgumentException("Failed to create hull");
      }

      var polygon = b2MakePolygon(ref hull, colliderMesh.BoundingBox.Min.X);
      // var polygon = b2MakeBox(colliderMesh.Height / 2, colliderMesh.Height / 2);

      return polygon;
    }
  }

  public object CreateAndAddBody(object settings) {
    var bodyDef = b2DefaultBodyDef();
    bodyDef.type = B2BodyType.b2_staticBody;

    var shapeDef = b2DefaultShapeDef();
    shapeDef.density = 1;
    shapeDef.material.friction = 0.3f;

    var polygon = (B2Polygon)settings;

    _bodyId = b2CreateBody(_worldId, ref bodyDef);
    _shapeId = b2CreatePolygonShape(_bodyId, ref shapeDef, ref polygon);

    return null!;
  }

  public void CreateAndAddBody(MotionType motionType, object shapeSettings, Vector2 position, bool isTrigger) {
    var bodyDef = b2DefaultBodyDef();
    switch (motionType) {
      case MotionType.Dynamic:
        bodyDef.type = B2BodyType.b2_dynamicBody;
        break;
      case MotionType.Static:
        bodyDef.type = B2BodyType.b2_staticBody;
        break;
      case MotionType.Kinematic:
        bodyDef.type = B2BodyType.b2_kinematicBody;
        break;
      default:
        break;
    }
    bodyDef.position = position.FromVec2;

    var shapeDef = b2DefaultShapeDef();
    shapeDef.density = 1;
    shapeDef.material.friction = 1.0f;

    _bodyId = b2CreateBody(_worldId, ref bodyDef);

    try {
      var polygons = (List<B2Polygon>)shapeSettings;

      foreach (var polygon in polygons) {
        var p = polygon;
        _shapeIndices.Add(b2CreatePolygonShape(_bodyId, ref shapeDef, ref p));
      }
    } catch {
      var polygon = (B2Polygon)shapeSettings;
      _shapeId = b2CreatePolygonShape(_bodyId, ref shapeDef, ref polygon);

      Logger.Info($"Created body {_bodyId.index1}");
    }
  }

  public void Dispose() {
    Logger.Info($"Disposing body {_bodyId.index1}");
    RemoveBody();
  }

  public void RemoveBody() {
    b2DestroyBody(_bodyId);
  }

  public void SetActive(bool value) {
    if (value) {
      b2Body_Enable(_bodyId);
    } else {
      b2Body_Disable(_bodyId);
    }
  }

  internal void Update() {
    // var cap = b2Body_GetContactCapacity(_bodyId);
    // var cap = b2Shape_GetContactCapacity(_shapeId);

    // Logger.Info($"[{_bodyId.index1}] - {cap}");
    // b2Body_GetContactData()
  }
}

internal static class Box2DExtensions {
  extension(B2Vec2 vec2) {
    internal Vector2 ToVec2 {
      get => new(vec2.X, vec2.Y);
    }
  }

  extension(Vector2 vec2) {
    internal B2Vec2 FromVec2 {
      get => new(vec2.X, vec2.Y);
    }
  }

  extension(float f1) {
    internal Vector2 ToVec2 {
      get => new(f1, f1);
    }
  }
}