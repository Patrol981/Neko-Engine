using System.Diagnostics;
using System.Numerics;
using Dwarf.EntityComponentSystem;
using Dwarf.Extensions.Logging;
using Dwarf.Globals;
using Dwarf.Rendering;
using Dwarf.Rendering.Renderer3D;

namespace Dwarf.Math;

public static class Frustum {
  public enum FrustumItersectionInfo {
    Inside,
    Outside,
    Intersecting
  }
  public static List<IRender3DElement> FilterObjectsByPlanes(in Plane[] planes, Span<IRender3DElement> objects) {
    var filteredObjects = new List<IRender3DElement>();

    // foreach (var obj in objects) {
    //   // var aabb = obj.GetOwner().GetComponent<MeshRenderer>().AABB;
    //   // if (IsInAABBFrustum(
    //   //     planes,
    //   //     aabb.Min,
    //   //     aabb.Max
    //   //   )
    //   // ) {
    //   //   filteredObjects.Add(obj);
    //   // }
    //   if (IsInSphereFrustumOG(
    //     planes,
    //     obj.Owner.GetComponent<Transform>().Position,
    //     obj.
    //     )
    //   ) {
    //     filteredObjects.Add(obj);
    //   }
    // }

    return filteredObjects;
  }

  public static void FlattenNodes<T>(Span<T> entities, out List<Node> nodes) where T : IRender3DElement {
    nodes = [];

    foreach (var entity in entities) {
      nodes.AddRange([.. entity.MeshedNodes]);
    }
  }

  public static void FilterNodesByPlanes(in Plane[] planes, in List<Node> inNodes, out List<Node> outNodes) {
    outNodes = [];
    // Guizmos.Clear();
    // foreach (var node in inNodes) {
    //   var owner = node.ParentRenderer.Owner;
    //   if (owner.CanBeDisposed) continue;
    //   var transform = owner.TryGetComponent<Transform>();
    //   Debug.Assert(transform != null);

    //   // var globalScale = node.Scale;
    //   // var transformedCenter = Vector4.Transform(new Vector4(node.Center, 1f), node.GetMatrix());
    //   // var globalCenter = new Vector3(transformedCenter.X, transformedCenter.Y, transformedCenter.Z);

    //   var position = (-node.GetMatrix().Translation + transform.Position) - node.Center * 2;

    //   // float maxScale = MathF.Max(MathF.Max(globalScale.X, globalScale.Y), globalScale.Z);

    //   if (node.Radius < 10) {
    //     // Guizmos.AddCircular(position, new(node.Radius * 2), new(0, 0.7f, 0));
    //   }


    //   // var result =
    //   //   IsOnOrForwardPlane(planes[5], position, node.Radius);
    //   // // IsOnOrForwardPlane(planes[4], globalCenter, node.Radius * (maxScale * 0.5f));
    //   // // IsOnOrForwardPlane(planes[2], globalCenter, node.Radius * (maxScale * 0.5f)) &&
    //   // // IsOnOrForwardPlane(planes[3], globalCenter, node.Radius * (maxScale * 0.5f)) &&
    //   // // IsOnOrForwardPlane(planes[4], globalCenter, node.Radius * (maxScale * 0.5f)) &&
    //   // // IsOnOrForwardPlane(planes[5], globalCenter, node.Radius * (maxScale * 0.5f));

    //   // if (result) {
    //   //   outNodes.Add(node);
    //   // }

    //   var result = IsInSphereFrustum(planes, position, node.Radius * 2);
    //   if (result == FrustumItersectionInfo.Inside || result == FrustumItersectionInfo.Intersecting) {
    //     outNodes.Add(node);
    //   }
    //   // if (IsBoundingSphereInFrustum(planes, node.Center + transform.Position, node.Radius)) {
    //   //   outNodes.Add(node);
    //   // }
    //   // if (IsInSphereFrustum(planes, position, node.Radius)) {
    //   //   outNodes.Add(node);
    //   // }
    //   // if (IsInAABBFrustum(planes, node.BoundingVolume.Min * node.GetMatrix().Translation, node.BoundingVolume.Max * node.GetMatrix().Translation)) {
    //   //   outNodes.Add(node);
    //   // }
    // }
  }

  public static void FilterNodesByFog(in List<Node> inNodes, out List<Node> outNodes) {
    outNodes = [];
    var globalUbo = Application.Instance.GlobalUbo;
    var iep = globalUbo.CameraPosition;
    var fogValue = Application.Instance.FogValue.X;
    foreach (var node in inNodes) {
      var owner = node.ParentRenderer.Owner;
      if (owner.CanBeDisposed) continue;
      var transform = owner.GetTransform();
      Debug.Assert(transform != null);
      var matrix = node.GetMatrix() * transform.Rotation() * transform.Position() * transform.Scale();
      var position = matrix.Translation;
      if (Vector2.Distance(new(position.X, position.Z), new(iep.X, iep.Z)) <= fogValue + (node.Radius * 4)) {
        outNodes.Add(node);
      }
    }
  }

  private static bool IsOnOrForwardPlane(Plane plane, Vector3 center, float radius) {
    return GetSignedDistanceToPlane(plane, center) > radius;
  }

  private static float GetSignedDistanceToPlane(Plane plane, Vector3 point) {
    return Vector3.Dot(plane.Normal, point) - plane.D;
  }

  public static void GetFrustrum(out Plane[] planes) {
    planes = new Plane[6];

    var camera = CameraState.GetCamera();
    Matrix4x4 projectionMatrix = camera.GetProjectionMatrix();
    Matrix4x4 viewMatrix = camera.GetViewMatrix();
    Matrix4x4 mvpMatrix = Matrix4x4.Multiply(viewMatrix, projectionMatrix);

    // Right plane
    planes[0] = new Plane(
      mvpMatrix.M14 + mvpMatrix.M11,
      mvpMatrix.M24 + mvpMatrix.M21,
      mvpMatrix.M34 + mvpMatrix.M31,
      mvpMatrix.M44 + mvpMatrix.M41
    );

    // Left plane
    planes[1] = new Plane(
      mvpMatrix.M14 - mvpMatrix.M11,
      mvpMatrix.M24 - mvpMatrix.M21,
      mvpMatrix.M34 - mvpMatrix.M31,
      mvpMatrix.M44 - mvpMatrix.M41
    );

    // Bottom plane
    planes[2] = new Plane(
      mvpMatrix.M14 + mvpMatrix.M12,
      mvpMatrix.M24 + mvpMatrix.M22,
      mvpMatrix.M34 + mvpMatrix.M32,
      mvpMatrix.M44 + mvpMatrix.M42
    );

    // Top plane
    planes[3] = new Plane(
      mvpMatrix.M14 - mvpMatrix.M12,
      mvpMatrix.M24 - mvpMatrix.M22,
      mvpMatrix.M34 - mvpMatrix.M32,
      mvpMatrix.M44 - mvpMatrix.M42
    );

    // Far plane
    planes[4] = new Plane(
      mvpMatrix.M13,
      mvpMatrix.M23,
      mvpMatrix.M33,
      mvpMatrix.M43
    );

    // Near plane
    planes[5] = new Plane(
      mvpMatrix.M14 + mvpMatrix.M13,
      mvpMatrix.M24 + mvpMatrix.M23,
      mvpMatrix.M34 + mvpMatrix.M33,
      mvpMatrix.M44 + mvpMatrix.M43
    );

    // Normalize all planes
    for (int i = 0; i < 6; i++) {
      planes[i] = Plane.Normalize(planes[i]);
    }

    // Logger.Info($"{camera.Far} {planes[4].D}");
  }

  public static void GetFrustrumBruh(out Plane[] planes) {
    var camera = CameraState.GetCamera();
    var viewProjection = camera.GetProjectionMatrix() * camera.GetViewMatrix();
    var camPos = CameraState.GetCameraEntity().GetTransform()!.Position;
    var vertical = camera.Far * MathF.Atan(camera.Fov * 0.5f);
    var horizontal = vertical * camera.Aspect;
    var multiplier = camera.Far * camera.Front;

    planes = new Plane[6];

    /*
    frustum.leftFace = {
      cam.Position,
      glm::cross(cam.Up,frontMultFar + cam.Right * halfHSide)
    };

    frustum.rightFace = {
      cam.Position,
      glm::cross(frontMultFar - cam.Right * halfHSide, cam.Up)
    };

    frustum.topFace = {
      cam.Position,
      glm::cross(cam.Right, frontMultFar - cam.Up * halfVSide)
    };

    frustum.bottomFace = {
      cam.Position,
      glm::cross(frontMultFar + cam.Up * halfVSide, cam.Right)
    };

    frustum.nearFace = {
      cam.Position + zNear * cam.Front,
      cam.Front
    };

    frustum.farFace = {
    cam.Position + frontMultFar,
    -cam.Front
    };
    */

    // Left Plane
    planes[0] = new Plane(
      camPos,
      Vector3.Dot(camera.Up, multiplier + camera.Right * horizontal)
    );

    // Right Plane
    planes[1] = new Plane(
      camPos,
      Vector3.Dot(multiplier - camera.Right * horizontal, camera.Up)
    );

    // Top Plane
    planes[2] = new Plane(
      camPos,
      Vector3.Dot(camera.Right, multiplier - camera.Up * vertical)
    );

    // Bottom Plane
    planes[3] = new Plane(
      camPos,
      Vector3.Cross(multiplier + camera.Up * vertical, camera.Right).Length()
    );

    // Near Plane
    planes[4] = new Plane(
      camPos + camera.Front * camera.Near,
      camera.Front.Length()
    );

    // Far Plane
    planes[5] = new Plane(
      camPos + camera.Front * camera.Far,
      -camera.Front.Length()
    );

    // Guizmos.Clear();
    // Guizmos.AddCircular(camPos - planes[5].Normal * planes[5].D, default, new(1, 0, 1));
  }

  public static void GetFrustrumNG(out Plane[] planes) {
    var camera = CameraState.GetCamera();
    var viewProjection = camera.GetViewMatrix() * camera.GetProjectionMatrix();

    planes = new Plane[6];

    // Left Plane
    planes[0] = new Plane(
        new Vector3(
            viewProjection.M14 + viewProjection.M11,
            viewProjection.M24 + viewProjection.M21,
            viewProjection.M34 + viewProjection.M31
        ),
        viewProjection.M44 + viewProjection.M41
    );

    // Right Plane
    planes[1] = new Plane(
        new Vector3(
            viewProjection.M14 - viewProjection.M11,
            viewProjection.M24 - viewProjection.M21,
            viewProjection.M34 - viewProjection.M31
        ),
        viewProjection.M44 - viewProjection.M41
    );

    // Bottom Plane (Swapped because your up is -Y)
    planes[2] = new Plane(
        new Vector3(
            viewProjection.M14 - viewProjection.M12, // Swap signs due to -Y up vector
            viewProjection.M24 - viewProjection.M22,
            viewProjection.M34 - viewProjection.M32
        ),
        viewProjection.M44 - viewProjection.M42
    );

    // Top Plane (Swapped because your up is -Y)
    planes[3] = new Plane(
        new Vector3(
            viewProjection.M14 + viewProjection.M12, // Swap signs due to -Y up vector
            viewProjection.M24 + viewProjection.M22,
            viewProjection.M34 + viewProjection.M32
        ),
        viewProjection.M44 + viewProjection.M42
    );

    // Near Plane
    planes[4] = new Plane(
        new Vector3(
            viewProjection.M14 + viewProjection.M13,
            viewProjection.M24 + viewProjection.M23,
            viewProjection.M34 + viewProjection.M33
        ),
        viewProjection.M44 + viewProjection.M43
    );

    // Far Plane
    planes[5] = new Plane(
        new Vector3(
            viewProjection.M14 - viewProjection.M13,
            viewProjection.M24 - viewProjection.M23,
            viewProjection.M34 - viewProjection.M33
        ),
        viewProjection.M44 - viewProjection.M43
    );

    // var camPos = CameraState.GetCameraEntity().GetComponent<Transform>().Position;
    // Guizmos.Clear();

    // Normalize planes to ensure correct culling behavior
    for (int i = 0; i < 6; i++) {
      planes[i] = NormalizePlane(planes[i]);
    }

    // Guizmos.AddCircular(planes[4].Normal, default, new(1, 0, 0));
    // Guizmos.AddCircular(camPos + planes[5].Normal * planes[5].D, default, new(1, 0, 1));
  }

  public static void GetFrustrumOG(out Plane[] planes) {
    var camera = CameraState.GetCamera();
    var viewProjection = camera.GetViewMatrix() * camera.GetProjectionMatrix();

    planes = new Plane[6];

    // Left Plane
    planes[0] = new Plane(
        new Vector3(
            viewProjection.M14 + viewProjection.M11,
            viewProjection.M24 + viewProjection.M21,
            viewProjection.M34 + viewProjection.M31
        ),
        viewProjection.M44 - viewProjection.M41
    );

    // Right Plane
    planes[1] = new Plane(
        new Vector3(
            viewProjection.M14 - viewProjection.M11,
            viewProjection.M24 - viewProjection.M21,
            viewProjection.M34 - viewProjection.M31
        ),
        viewProjection.M44 + viewProjection.M41
    );

    // Bottom Plane
    planes[2] = new Plane(
        new Vector3(
            viewProjection.M14 - viewProjection.M12,
            viewProjection.M24 - viewProjection.M22,
            viewProjection.M34 - viewProjection.M32
        ),
        viewProjection.M44 - viewProjection.M42
    );

    // Top Plane
    planes[3] = new Plane(
        new Vector3(
            viewProjection.M14 + viewProjection.M12,
            viewProjection.M24 + viewProjection.M22,
            viewProjection.M34 + viewProjection.M32
        ),
        viewProjection.M44 + viewProjection.M42
    );

    // Near Plane
    planes[4] = new Plane(
        new Vector3(
          viewProjection.M14 + viewProjection.M13,
          viewProjection.M24 + viewProjection.M23,
          viewProjection.M34 + viewProjection.M33
        ),
        viewProjection.M44 + viewProjection.M43
    );

    // Far Plane
    planes[5] = new Plane(
        new Vector3(
            viewProjection.M14 - viewProjection.M13,
            viewProjection.M24 - viewProjection.M23,
            viewProjection.M34 - viewProjection.M33
        ),
        viewProjection.M44 - viewProjection.M43
    );

    var camPos = CameraState.GetCameraEntity().GetTransform()!.Position;
    Guizmos.Clear();

    // Guizmos.AddCircular(camPos - (planes[5].Normal * planes[5].D), default, new(1, 0, 1));

    // Normalize planes
    for (int i = 0; i < planes.Length; i++) {
      planes[i] = NormalizePlane(planes[i]);
    }


    // Guizmos.AddCircular(camPos + planes[4].Normal, new(0.2f), new(1, 0, 0));
    // Guizmos.AddCircular(camPos - (planes[5].Normal), default, new(1, 0, 1));
  }

  private static Plane NormalizePlane(Plane plane) {
    float magnitude = plane.Normal.LengthSquared();
    return new Plane(plane.Normal / magnitude, plane.D / magnitude);
  }

  public static bool IsBoundingSphereInFrustum(in Plane[] planes, Vector3 center, float radius) {
    foreach (var plane in planes) {
      if (Plane.DotCoordinate(plane, center) < -(radius * radius)) {
        return false; // Outside frustum
      }
    }
    return true; // Inside frustum
  }


  public static FrustumItersectionInfo IsInSphereFrustum(in Plane[] planes, Vector3 center, float radius) {
    int outsideCount = 0;

    foreach (var plane in planes) {
      // Calculate the distance from the sphere's center to the plane
      float distance = Vector3.Cross(plane.Normal, center).Length() + plane.D; // Plane equation: Ax + By + Cz + D = 0
      // var distance = Vector3.Distance(plane.Normal * plane.D, center);

      // If the distance is greater than the radius, the sphere is outside the frustum
      if (distance < -radius) {
        return FrustumItersectionInfo.Outside;
      }

      // If the distance is between the negative radius and positive radius, the sphere intersects the frustum
      if (distance < radius) {
        outsideCount++;
      }
    }

    // If no planes are intersected, the sphere is inside the frustum
    if (outsideCount == 0) {
      return FrustumItersectionInfo.Inside;
    }

    // If some planes are intersected, it is considered as intersecting
    return FrustumItersectionInfo.Intersecting;
  }

  public static bool IsInSphereFrustumOG(in Plane[] planes, Vector3 center, float radius) {
    for (int i = 0; i < planes.Length; i++) {
      // float distance = Vector3.Dot(planes[i].Normal, center) + planes[i].D;
      float distance = Vector3.Dot(planes[i].Normal, center) + planes[i].D;
      // float result = distance + radius;
      Logger.Info(distance);
      var pos = Vector3.Cross(planes[i].Normal, center);
      // float distance = planes[i].D + radius;
      if (distance < -radius) {
        return false;
      }
    }
    return true;
  }

  public static bool IsInAABBFrustum(in Plane[] planes, Vector3 min, Vector3 max) {
    for (int i = 0; i < planes.Length; i++) {
      var vec3 = new Vector3(
        planes[i].Normal.X >= 0 ? max.X : min.X,
        planes[i].Normal.Y >= 0 ? max.Y : min.Y,
        planes[i].Normal.Z >= 0 ? max.Z : min.Z
      );
      if (Vector3.Dot(planes[i].Normal, vec3) + planes[i].D < -float.Epsilon)
        return false;
    }
    return true;
  }
}