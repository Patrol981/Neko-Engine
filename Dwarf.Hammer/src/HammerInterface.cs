using System.Numerics;
using Dwarf.Hammer.Enums;
using Dwarf.Hammer.Models;
using Dwarf.Hammer.Structs;

namespace Dwarf.Hammer;

public class HammerInterface {
  private readonly HammerWorld _hammerWorld;

  public HammerInterface(HammerWorld hammerWorld) {
    _hammerWorld = hammerWorld;
  }

  public Vector2 GetPosition(in BodyId bodyId) {
    return _hammerWorld.Bodies[bodyId].Position;
  }

  public void SetPosition(in BodyId bodyId, in Vector2 position) {
    _hammerWorld.Bodies[bodyId].Position = position;
  }

  public void AddVelocity(in BodyId bodyId, in Vector2 velocity) {
    _hammerWorld.Bodies[bodyId].Velocity += velocity;
  }

  public void SetVelocity(in BodyId bodyId, in Vector2 velocity) {
    _hammerWorld.Bodies[bodyId].Velocity = velocity;
  }

  public Vector2 GetVelocity(in BodyId bodyId) {
    return _hammerWorld.Bodies[bodyId].Velocity;
  }

  public void AddForce(in BodyId bodyId, in Vector2 force) {
    _hammerWorld.Bodies[bodyId].Force += force;
  }

  public void SetGravity(float gravity) {
    _hammerWorld.Gravity = gravity;
  }

  public float GetGravity() {
    return _hammerWorld.Gravity;
  }

  public void SetMotionType(in BodyId bodyId, MotionType motionType) {
    _hammerWorld.Bodies[bodyId].MotionType = motionType;
  }

  public MotionType GetMotionType(in BodyId bodyId) {
    return _hammerWorld.Bodies[bodyId].MotionType;
  }

  public void SetMotionQuality(in BodyId bodyId, MotionQuality motionQuality) {
    _hammerWorld.Bodies[bodyId].MotionQuality = motionQuality;
  }

  public MotionQuality GetMotionQuality(in BodyId bodyId) {
    return _hammerWorld.Bodies[bodyId].MotionQuality;
  }

  public BodyId CreateAndAddBody(ShapeSettings shapeSettings, MotionType motionType, Vector2 position, bool isTrigger) {
    var body = _hammerWorld.AddBody(position);
    _hammerWorld.Bodies[body].MotionType = motionType;
    _hammerWorld.Bodies[body].Mesh = shapeSettings.Mesh;
    _hammerWorld.Bodies[body].ObjectType = shapeSettings.ObjectType;
    _hammerWorld.Bodies[body].IsTrigger = isTrigger;
    if (shapeSettings.ObjectType == ObjectType.Sprite && shapeSettings.UserData != null) {
      var minMax = ((Vector2, Vector2))shapeSettings.UserData;
      _hammerWorld.Bodies[body].AABB = new AABB() { Min = minMax.Item1, Max = minMax.Item2 };
    } else {
      try {
        var edges = (List<Edge>)shapeSettings.UserData!;
        _hammerWorld.Bodies[body].AABB = AABB.ComputeAABB(_hammerWorld.Bodies[body]);
        _hammerWorld.Bodies[body].Edges = [.. edges];
      } catch {
        var aabbs = (List<(Vector2, Vector2)>)shapeSettings.UserData!;
        _hammerWorld.Bodies[body].TilemapAABBs = AABB.CreateAABBListFromTilemap(aabbs);
        // _hammerWorld.Bodies[body].TilemapAABBs = [.. AABB.BuildAABBsFromMesh(_hammerWorld.Bodies[body].Mesh, _hammerWorld.Bodies[body].Position)];
        // _hammerWorld.Bodies[body].TilemapAABBs = [.. aabbs.Select(x => {
        //   return new AABB() {
        //     Min = x.Item1,
        //     Max = x.Item2
        //   };
        // })];
      }
    }
    return body;
  }

  public void RemoveBody(BodyId bodyId) {
    _hammerWorld.RemoveBody(bodyId);
  }
}