using System.Numerics;

using Dwarf.EntityComponentSystem;
using Dwarf.Globals;
using Dwarf.Rendering.Renderer3D;

namespace Dwarf.Math;

public class Ray {
  public class RayResult {
    public Vector3 RayOrigin { get; set; }
    public Vector3 RayDirection { get; set; }
    public Vector3 RayOriginRaw { get; set; }
    public Vector3 RayDirectionRaw { get; set; }
  }

  public class RaycastHitResult {
    public bool Present { get; set; }
    public Vector3 Point { get; set; }
    public float Distance { get; set; }
  }

  public record Plane(Vector3 Normal, Vector3 Point);

  public static Vector2 MouseToWorld2D(Camera camera, Vector2 screenSize, float planeZ = 0f) {
    var mouse = Input.MousePosition;

    float ndcX = 2f * (float)mouse.X / screenSize.X - 1f;
    float ndcY = 2f * (float)mouse.Y / screenSize.Y - 1f;

    var nearClip = new Vector4(ndcX, ndcY, 0f, 1f);
    var farClip = new Vector4(ndcX, ndcY, 1f, 1f);

    var proj = camera.GetProjectionMatrix();
    var view = camera.GetViewMatrix();

    if (!Matrix4x4.Invert(proj, out var invProj) ||
        !Matrix4x4.Invert(view, out var invView))
      return new Vector2(float.NaN, float.NaN);

    Vector4 n = Vector4.Transform(nearClip, invProj);
    n /= n.W;
    n = Vector4.Transform(n, invView);
    n /= n.W;

    Vector4 f = Vector4.Transform(farClip, invProj);
    f /= f.W;
    f = Vector4.Transform(f, invView);
    f /= f.W;

    Vector3 nearW = new Vector3(n.X, n.Y, n.Z);
    Vector3 farW = new Vector3(f.X, f.Y, f.Z);

    Vector3 dir = Vector3.Normalize(farW - nearW);

    const float EPS = 1e-6f;
    float denom = Vector3.Dot(dir, new Vector3(0f, 0f, 1f));
    if (MathF.Abs(denom) < EPS) return new Vector2(float.NaN, float.NaN);

    float t = (planeZ - nearW.Z) / denom;
    Vector3 hit = nearW + t * dir;

    return new Vector2(hit.X, hit.Y);
  }

  public static RayResult GetRayInfo(Camera camera, Vector2 screenSize) {
    var mousePos = Input.MousePosition;
    var rayStartNDC = new Vector4(
      ((float)mousePos.X / screenSize.X - 0.5f) * 2.0f,
      ((float)mousePos.Y / screenSize.Y - 0.5f) * 2.0f,
      -1.0f,
      1.0f
    );
    var rayEndNDC = new Vector4(
      ((float)mousePos.X / screenSize.X - 0.5f) * 2.0f,
      ((float)mousePos.Y / screenSize.Y - 0.5f) * 2.0f,
      0.0f,
      1.0f
    );

    Matrix4x4.Invert(camera.GetProjectionMatrix(), out var inverseProjection);
    Matrix4x4.Invert(camera.GetViewMatrix(), out var inverseViewMatrix);

    var rayStartCamera = Vector4.Transform(rayStartNDC, inverseProjection);
    rayStartCamera /= rayStartCamera.W;

    var rayStartWorld = Vector4.Transform(rayStartCamera, inverseViewMatrix);
    rayStartWorld /= rayStartWorld.W;

    var rayEndCamera = Vector4.Transform(rayEndNDC, inverseProjection);
    rayEndCamera /= rayEndCamera.W;

    var rayEndWorld = Vector4.Transform(rayEndCamera, inverseViewMatrix);
    rayEndWorld /= rayEndWorld.W;

    var rayDirWorld = rayEndWorld - rayStartWorld;
    rayDirWorld = Vector4.Normalize(rayDirWorld);

    var result = new RayResult {
      RayOrigin = new(rayStartWorld.X, rayStartWorld.Y, rayStartWorld.Z),
      RayDirection = Vector3.Normalize(new(rayDirWorld.X, rayDirWorld.Y, rayDirWorld.Z)),
      RayOriginRaw = new(rayStartWorld.X, rayStartWorld.Y, rayStartWorld.Z),
      RayDirectionRaw = new(rayDirWorld.X, rayDirWorld.Y, rayDirWorld.Z)
    };

    return result;
  }

  public static RaycastHitResult CastRay(float maxDistance) {
    var camera = CameraState.GetCamera();
    var screenSize = Application.Instance.Window.Extent;

    var rayData = GetRayInfo(camera, new(screenSize.Width, screenSize.Height));

    var hitResult = new RaycastHitResult {
      Present = false,
      Point = rayData.RayOrigin + rayData.RayDirection * maxDistance
    };

    return hitResult;
  }

  public static ReadOnlySpan<Entity> Raycast(AABBFilter aabbFilter = AABBFilter.None) {
    var entities = Application.Instance.GetEntities();
    var result = new Dictionary<Entity, RaycastHitResult>();

    foreach (var entity in entities.Where(x => !x.CanBeDisposed)) {
      var enTransform = entity.TryGetComponent<Transform>();
      var enDistance = Vector3.Distance(
        CameraState.GetCameraEntity().GetComponent<Transform>().Position,
        enTransform != null ? enTransform.Position : Vector3.Zero
      );

      var enResult = Ray.CastRayIntersect(entity, enDistance, aabbFilter);
      if (enResult.Present) {
        result.TryAdd(entity, enResult);
      }
    }

    return result
      .OrderBy(pair => pair.Value.Distance)
      .Select(pair => pair.Key)
      .ToArray();
  }

  public static ReadOnlySpan<KeyValuePair<Entity, RaycastHitResult>> RaycastWithRayInfo(AABBFilter aabbFilter = AABBFilter.None) {
    var entities = Application.Instance.GetEntities();
    var result = new Dictionary<Entity, RaycastHitResult>();

    for (int i = 0; i < entities.Count; i++) {
      if (entities[i].CanBeDisposed) continue;
      var enTransform = entities[i].TryGetComponent<Transform>();
      var enDistance = Vector3.Distance(
        CameraState.GetCameraEntity().GetComponent<Transform>().Position,
        enTransform != null ? enTransform.Position : Vector3.Zero
      );

      var enResult = Ray.CastRayIntersect(entities[i], enDistance, aabbFilter);
      if (enResult.Present) {
        result.TryAdd(entities[i], enResult);
      }
    }

    return new ReadOnlySpan<KeyValuePair<Entity, RaycastHitResult>>(
        [.. result.OrderBy(pair => pair.Value.Distance)]
    );
  }

  public static RaycastHitResult CastRayIntersect(Entity entity, float maxDistance, AABBFilter aabbFilter = AABBFilter.None) {
    var camera = CameraState.GetCamera();
    var screenSize = Application.Instance.Window.Extent;

    var rayData = GetRayInfo(camera, new(screenSize.Width, screenSize.Height));

    var transform = entity.GetComponent<Transform>();
    var model = entity.GetComponent<MeshRenderer>();

    var collisionPoint = Vector3.Zero;
    var hitResult = new RaycastHitResult {
      Present = false,
      Point = collisionPoint
    };

    if (model == null || transform == null) return hitResult;
    if (model.AABBFilter != aabbFilter && model.AABBFilter != AABBFilter.None) return hitResult;

    var modelMatrix = transform.Matrix4;
    var positionWorldspace = new Vector3(modelMatrix[3, 0], modelMatrix[3, 1], modelMatrix[3, 2]);
    var delta = positionWorldspace - rayData.RayOrigin;

    return AABBIntersection(hitResult, modelMatrix, rayData, delta, model, maxDistance);
  }

  private static RaycastHitResult AABBIntersection(
    RaycastHitResult hitResult,
    Matrix4x4 modelMatrix,
    RayResult rayData,
    Vector3 delta,
    MeshRenderer model,
    float maxDistance
  ) {
    if (model == null) return hitResult;

    float tMin = 0.0f;
    float tMax = maxDistance;

    {
      var xAxis = new Vector3(modelMatrix[0, 0], modelMatrix[0, 1], modelMatrix[0, 2]);
      float e = Vector3.Dot(xAxis, delta);
      float f = Vector3.Dot(rayData.RayDirection, xAxis);

      if (MathF.Abs(f) > 0.001) {
        float t1 = (e + model.AABB.Min.X) / f;
        float t2 = (e + model.AABB.Max.X) / f;

        if (t1 > t2) {
          (t2, t1) = (t1, t2);
        }

        if (t2 < tMax) {
          hitResult.Point = rayData.RayOrigin + rayData.RayDirection * t2;
          tMax = t2;
        }

        if (t1 > tMin) {
          hitResult.Point = rayData.RayOrigin + rayData.RayDirection * t1;
          tMin = t1;
        }

        if (tMax < tMin)
          return hitResult;
      } else {
        if (-e + model.AABB.Min.X > 0.0f || -e + model.AABB.Max.X < 0.0f) {
          return hitResult;
        }
      }
    }

    {
      var yAxis = new Vector3(modelMatrix[1, 0], modelMatrix[1, 1], modelMatrix[1, 2]);
      float e = Vector3.Dot(yAxis, delta);
      float f = Vector3.Dot(rayData.RayDirection, yAxis);

      if (MathF.Abs(f) > 0.001) {
        float t1 = (e + model.AABB.Min.Y) / f;
        float t2 = (e + model.AABB.Max.Y) / f;

        if (t1 > t2) {
          (t2, t1) = (t1, t2);
        }

        if (t2 < tMax) {
          hitResult.Point = rayData.RayOrigin + rayData.RayDirection * t2;
          tMax = t2;
        }

        if (t1 > tMin) {
          hitResult.Point = rayData.RayOrigin + rayData.RayDirection * t1;
          tMin = t1;
        }

        if (tMin > tMax)
          return hitResult;
      } else {
        if (-e + model.AABB.Min.Y > 0.0f || -e + model.AABB.Max.Y < 0.0f) {
          return hitResult;
        }
      }
    }

    {
      var zAxis = new Vector3(modelMatrix[2, 0], modelMatrix[2, 1], modelMatrix[2, 2]);
      float e = Vector3.Dot(zAxis, delta);
      float f = Vector3.Dot(rayData.RayDirection, zAxis);

      if (MathF.Abs(f) > 0.001) {
        float t1 = (e + model.AABB.Min.Z) / f;
        float t2 = (e + model.AABB.Max.Z) / f;
        if (t1 > t2) {
          (t2, t1) = (t1, t2);
        }

        if (t2 < tMax) {
          hitResult.Point = rayData.RayOrigin + rayData.RayDirection * t2;
          tMax = t2;
        }

        if (t1 > tMin) {
          hitResult.Point = rayData.RayOrigin + rayData.RayDirection * t1;
          tMin = t1;
        }

        if (tMin > tMax)
          return hitResult;
      } else {
        if (-e + model.AABB.Min.Z > 0.0f || -e + model.AABB.Max.Z < 0.0f) {
          return hitResult;
        }
      }
    }

    hitResult.Present = true;
    hitResult.Distance = tMin;
    return hitResult;
  }


  [Obsolete]
  public static Vector2 ScreenPointToWorld2D(Camera camera, Vector2 point, Vector2 screenSize) {
    float normalizedX = 2.0f * point.X / screenSize.X - 1.0f;
    float normalizedY = 1.0f - 2.0f * point.Y / screenSize.Y;

    Matrix4x4.Invert(camera.GetProjectionMatrix(), out var unProject);
    Vector4 nearPoint = new(normalizedX, normalizedY, 0.0f, 1.0f);
    Vector4 worldPoint = Vector4.Transform(nearPoint, unProject);
    var tmp = new Vector2 {
      X = worldPoint.X / worldPoint.W,
      Y = worldPoint.Y / worldPoint.W
    };

    tmp.X *= 100;
    tmp.Y *= -100;
    return tmp;
  }

  [Obsolete]
  public static Vector2 WorldToScreenPoint(Camera camera, Vector3 point, Vector2 screenSize) {
    var vp = camera.GetViewMatrix() * camera.GetProjectionMatrix();

    Vector4 clipSpace = Vector4.Transform(new Vector4(point, 1.0f), vp);

    if (MathF.Abs(clipSpace.W) < 1e-6f || clipSpace.W < 0.0f) {
      return new Vector2(float.NaN, float.NaN);
    }

    var ndc = new Vector3(
      clipSpace.X / clipSpace.W,
      clipSpace.Y / clipSpace.W,
      clipSpace.Z / clipSpace.W
    );

    float screenX = (ndc.X + 1.0f) * 0.5f * screenSize.X;
    float screenY = (1.0f + ndc.Y) * 0.5f * screenSize.Y;

    return new Vector2(screenX, screenY);
  }
}