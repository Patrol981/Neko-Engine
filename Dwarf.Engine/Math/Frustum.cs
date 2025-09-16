using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using Dwarf.EntityComponentSystem;
using Dwarf.Extensions.Logging;
using Dwarf.Globals;
using Dwarf.Rendering;
using Dwarf.Rendering.Renderer3D;

namespace Dwarf.Math;

public static class Frustum {
  public enum FrustumContainment {
    Outside = 0,
    Intersecting = 1,
    Inside = 2
  }

  public enum MatrixOrder { RowMajor, ColumnMajor }
  public enum ClipSpaceDepth { ZeroToOne, MinusOneToOne }

  public struct Plane {
    public Vector3 Normal;
    public float D;

    public Plane(Vector3 n, float d) {
      Normal = n;
      D = d;
    }

    public void Normalize() {
      float len = Normal.Length();
      if (len > 1e-8f) {
        float inv = 1.0f / len;
        Normal *= len;
        D *= inv;
      }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float DistanceTo(in Vector3 p) => Vector3.Dot(Normal, p) + D;
  }

  public struct FrustumPlanes {
    public Plane Left, Right, Bottom, Top, Near, Far;

    public readonly Plane this[int i] => i switch {
      0 => Left,
      1 => Right,
      2 => Bottom,
      3 => Top,
      4 => Near,
      5 => Far,
      _ => Left
    };
  }

  public static void FlattenNodes<T>(Span<T> entities, out List<Node> nodes) where T : IRender3DElement {
    nodes = [];

    foreach (var entity in entities) {
      if (entity.Owner.CanBeDisposed) continue;
      nodes.AddRange([.. entity.MeshedNodes]);
    }
  }

  public static void FlattenNodes<T>(T[] entities, out List<Node> nodes) where T : IRender3DElement {
    nodes = [];

    foreach (var entity in entities) {
      if (entity.Owner.CanBeDisposed) continue;
      nodes.AddRange([.. entity.MeshedNodes]);
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveOptimization)]
  public static void FilterNodesByFog(in List<Node> inNodes, out List<Node> outNodes) {
    outNodes = [];
    var globalUbo = Application.Instance.GlobalUbo;
    var iep = globalUbo.CameraPosition;
    var fogValue = Application.Instance.FogValue.X;
    foreach (var node in inNodes) {
      var owner = node.ParentRenderer.Owner;
      if (owner.CanBeDisposed) continue;
      var transform = owner.GetTransform();
      var matrix = node.GetMatrix() * transform!.Rotation() * transform!.Position() * transform!.Scale();
      var position = matrix.Translation;
      if (Vector2.Distance(new(position.X, position.Z), new(iep.X, iep.Z)) <= fogValue + (node.Radius * 4)) {
        outNodes.Add(node);
      }
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveOptimization)]
  public static FrustumPlanes BuildFromCamera(Camera cam) {
    // Use the camera's view matrix and projection matrix to calculate the frustum
    var view = cam.GetViewMatrix();
    var proj = cam.GetProjectionMatrix();

    // Multiply the view and projection matrices to get the combined view-projection matrix
    var vp = view * proj;

    // Extract the planes from the combined view-projection matrix
    return ExtractPlanes(vp, normalize: true);
  }

  // Method to extract the frustum planes from the view-projection matrix
  public static FrustumPlanes ExtractPlanes(
        in Matrix4x4 vp,
        bool normalize = true,
        MatrixOrder order = MatrixOrder.RowMajor,
        ClipSpaceDepth depth = ClipSpaceDepth.ZeroToOne
      ) {
    // If the incoming 'vp' is column-major (OpenGL style), transpose so we can
    // use the same row-major extraction math below.
    Matrix4x4 m = (order == MatrixOrder.ColumnMajor) ? Matrix4x4.Transpose(vp) : vp;

    // Common row-major extraction (Row4 ± RowX)
    Plane left = new(new Vector3(m.M14 + m.M11, m.M24 + m.M21, m.M34 + m.M31), m.M44 + m.M41);
    Plane right = new(new Vector3(m.M14 - m.M11, m.M24 - m.M21, m.M34 - m.M31), m.M44 - m.M41);
    Plane bottom = new(new Vector3(m.M14 + m.M12, m.M24 + m.M22, m.M34 + m.M32), m.M44 + m.M42);
    Plane top = new(new Vector3(m.M14 - m.M12, m.M24 - m.M22, m.M34 - m.M32), m.M44 - m.M42);

    // left.Normal *= -1;
    // right.Normal *= -1;

    Plane nearP, farP;

    if (depth == ClipSpaceDepth.ZeroToOne) {
      // D3D/Vulkan (z ∈ [0,1]): near = Row3, far = Row4 - Row3
      nearP = new Plane(new Vector3(m.M13, m.M23, m.M33), m.M43);
      farP = new Plane(new Vector3(m.M14 - m.M13, m.M24 - m.M23, m.M34 - m.M33), m.M44 - m.M43);
    } else {
      // OpenGL (z ∈ [-1,1]): near = Row4 + Row3, far = Row4 - Row3
      nearP = new Plane(new Vector3(m.M14 + m.M13, m.M24 + m.M23, m.M34 + m.M33), m.M44 + m.M43);
      farP = new Plane(new Vector3(m.M14 - m.M13, m.M24 - m.M23, m.M34 - m.M33), m.M44 - m.M43);
    }

    if (normalize) {
      left.Normalize(); right.Normalize(); bottom.Normalize();
      top.Normalize(); nearP.Normalize(); farP.Normalize();
    }

    return new FrustumPlanes { Left = left, Right = right, Bottom = bottom, Top = top, Near = nearP, Far = farP };
  }

  // Method to check if a bounding sphere is inside the frustum
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static bool SphereInside(in FrustumPlanes f, in Vector3 center, float radius) {
    // Check if the sphere is inside all 6 planes of the frustum
    if (f.Left.DistanceTo(center) < radius) return false;
    if (f.Right.DistanceTo(center) < radius) return false;
    if (f.Bottom.DistanceTo(center) < -radius) return false;
    if (f.Top.DistanceTo(center) < -radius) return false;
    if (f.Near.DistanceTo(center) < -radius) return false;
    if (f.Far.DistanceTo(center) < -radius) return false;

    return true;
  }

  // Method to extract the maximum scale from a transformation matrix
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  private static float ExtractMaxScale(in Matrix4x4 m) {
    // Extract the lengths of the basis vectors (ignoring translation)
    float sx = MathF.Sqrt(m.M11 * m.M11 + m.M12 * m.M12 + m.M13 * m.M13);
    float sy = MathF.Sqrt(m.M21 * m.M21 + m.M22 * m.M22 + m.M23 * m.M23);
    float sz = MathF.Sqrt(m.M31 * m.M31 + m.M32 * m.M32 + m.M33 * m.M33);
    return MathF.Max(sx, MathF.Max(sy, sz));
  }

  public static void FilterNodesByPlanes(in FrustumPlanes planes, in List<Node> inNodes, out List<Node> outNodes) {
    Guizmos.Clear();

    outNodes = new List<Node>(inNodes.Count);

    // Loop through all nodes and check if they are inside the frustum
    for (int i = 0; i < inNodes.Count; i++) {
      var node = inNodes[i];
      if (!node.Enabled || !node.HasMesh) continue;

      var owner = node.ParentRenderer.Owner;
      if (owner.CanBeDisposed || !owner.Active) continue;

      var t = owner.GetTransform();
      if (t == null) continue;

      // Calculate the world matrix of the node
      var world = node.GetMatrix() * t.Rotation() * t.Position() * t.Scale();

      // Get the center and radius of the node's bounding sphere (scaled by node's world transform)
      var center = world.Translation;
      float radiusScaled = MathF.Max(0.0001f, node.Radius * ExtractMaxScale(world));

      // Perform the sphere test against the frustum
      if (SphereInside(planes, center, radiusScaled)) {
        outNodes.Add(node);
      }

      // Guizmos.AddCircular(center, new(radiusScaled / 10, radiusScaled / 10, radiusScaled / 10), new(0, 0, 1));

    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static FrustumContainment ClassifySphere(in FrustumPlanes f, in Vector3 center, float radius, float epsilon = 0f) {
    // epsilon lets you be conservative (treat almost-touching as intersecting/inside)
    bool intersects = false;

    // Check each plane
    float d = f.Left.DistanceTo(center);
    if (d < -radius - epsilon) return FrustumContainment.Outside;
    if (d < radius + epsilon) intersects = true;

    d = f.Right.DistanceTo(center);
    if (d < -radius - epsilon) return FrustumContainment.Outside;
    if (d < radius + epsilon) intersects = true;

    d = f.Bottom.DistanceTo(center);
    if (d < -radius - epsilon) return FrustumContainment.Outside;
    if (d < radius + epsilon) intersects = true;

    d = f.Top.DistanceTo(center);
    if (d < -radius - epsilon) return FrustumContainment.Outside;
    if (d < radius + epsilon) intersects = true;

    d = f.Near.DistanceTo(center);
    if (d < -radius - epsilon) return FrustumContainment.Outside;
    if (d < radius + epsilon) intersects = true;

    d = f.Far.DistanceTo(center);
    if (d < -radius - epsilon) return FrustumContainment.Outside;
    if (d < radius + epsilon) intersects = true;

    return intersects ? FrustumContainment.Intersecting : FrustumContainment.Inside;
  }

}