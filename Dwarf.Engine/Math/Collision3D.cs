using System.Numerics;
using Dwarf.EntityComponentSystem;
using Dwarf.Extensions.Logging;
using Dwarf.Rendering.Renderer3D;

namespace Dwarf.Math;

public static class Collision3D {
  public static bool CollidesWith(this Entity entity, Entity targetEntity) {
    throw new NotImplementedException();
  }

  public static bool CheckSphere(Vector3 center, float radius, EntityLayer layerMask) {
    var collisionObjects = Application.Instance.GetEntitiesEnumerable().Where(x => x.Layer == layerMask);

    foreach (var coll in collisionObjects) {
      if (!coll.HasComponent<Transform>()) continue;

      var collTransform = coll.GetComponent<Transform>();
      var collMesh = coll.GetComponent<MeshRenderer>();

      var aabb = collMesh.AABB;
      float sqrDist = SquaredDistancePointAABB(
        center,
        aabb.Min + collTransform.Position,
        aabb.Max + collTransform.Position
      );

      var result = sqrDist <= radius * radius;
      if (!result) continue;
      else return result;
    }
    return false;
  }

  private static float SquaredDistancePointAABB(Vector3 point, Vector3 boundingMin, Vector3 boundingMax) {
    float sqrDist = 0.0f;

    for (int i = 0; i < 3; i++) {
      float v = point[i];
      float min = boundingMin[i];
      float max = boundingMax[i];

      if (v < min)
        sqrDist += (min - v) * (min - v);
      else if (v > max)
        sqrDist += (v - max) * (v - max);
    }

    return sqrDist;
  }
}
