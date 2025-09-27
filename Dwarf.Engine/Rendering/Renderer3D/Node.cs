using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Dwarf.AbstractionLayer;
using Dwarf.Math;
using Dwarf.Rendering.Renderer3D.Animations;

namespace Dwarf.Rendering.Renderer3D;

public struct NodeInfo {
  public Vector3 Translation;
  public Quaternion Rotation;
  public Vector3 Scale;

  public NodeInfo() {
    Translation = Vector3.Zero;
    Rotation = Quaternion.Identity;
    Scale = Vector3.Zero;
  }
}

public class Node : ICloneable, IDisposable, IComparable<Node> {
  private readonly Application _app;


  public int Index = 0;
  public string Name = string.Empty;
  public Node? Parent;
  public List<Node> Children = [];

  public Matrix4x4 NodeMatrix = Matrix4x4.Identity;

  public Guid MeshGuid = Guid.Empty;
  public Guid SkinGuid = Guid.Empty;
  public Guid EntityGuid = Guid.Empty;

  internal int SkinIndex = -1;

  public Vector3 Translation = Vector3.Zero;
  public Quaternion Rotation = Quaternion.Identity;
  public Vector3 Scale = Vector3.One;

  public Vector3 TranslationOffset = Vector3.Zero;
  public Quaternion RotationOffset = Quaternion.Identity;
  public Vector3 ScaleOffset = Vector3.One;

  public bool UseCachedMatrix = false;
  public Matrix4x4 CachedLocalMatrix = Matrix4x4.Identity;
  public Matrix4x4 CachedMatrix = Matrix4x4.Identity;


  public MeshRenderer ParentRenderer = null!;
  public BoundingBox BoundingVolume;
  public BoundingBox AABB;
  public float Radius = 0;
  public Vector3 Center { get; private set; }

  public bool Enabled { get; set; } = true;
  public bool FilterMeInShader { get; set; } = false;

  public float AnimationTimer = 0.0f;
  public Guid BatchId { get; init; } = Guid.NewGuid();

  public Node(
    Application app,
    Guid entityId
  ) {
    _app = app;
    EntityGuid = entityId;
  }

  [MethodImpl(MethodImplOptions.AggressiveOptimization)]
  public Matrix4x4 GetLocalMatrix() {
    if (!UseCachedMatrix) {
      bool hasBakedMatrix =
        !Matrix4x4.Identity.Equals(NodeMatrix);

      CachedLocalMatrix = hasBakedMatrix
        ? NodeMatrix
        : Matrix4x4.CreateScale(Scale)
          * Matrix4x4.CreateFromQuaternion(Rotation)
          * Matrix4x4.CreateTranslation(Translation);

      // CachedLocalMatrix =
      //   NodeMatrix *
      //   Matrix4x4.CreateScale(Scale) *
      //   Matrix4x4.CreateFromQuaternion(Rotation) *
      //   Matrix4x4.CreateTranslation(Translation);
    }
    return CachedLocalMatrix;
  }

  [MethodImpl(MethodImplOptions.AggressiveOptimization)]
  public Matrix4x4 GetMatrix() {
    if (!UseCachedMatrix) {
      Matrix4x4 m = GetLocalMatrix();
      var p = Parent;
      while (p != null) {
        m *= p.GetLocalMatrix();
        p = p.Parent;
      }
      CachedMatrix = m;
      UseCachedMatrix = true;
      return m;
    } else {
      return CachedMatrix;
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveOptimization)]
  public void Update() {
    UseCachedMatrix = false;
    if (MeshGuid != Guid.Empty) {
      Matrix4x4 m = GetMatrix();
      Skin? skin = null;
      Mesh? mesh = null;
      try {
        _app.Skins.TryGetValue(SkinGuid, out skin);
        _app.Meshes.TryGetValue(MeshGuid, out mesh);

        mesh!.Matrix = m;
      } catch { }
      if (skin != null) {
        Matrix4x4.Invert(m, out var inTransform);
        var jointsArr = CollectionsMarshal.AsSpan(skin.Joints);
        var invMatArr = CollectionsMarshal.AsSpan(skin.InverseBindMatrices);

        int numJoints = (int)MathF.Min(jointsArr.Length, CommonConstants.MAX_NUM_JOINTS);
        for (int i = 0; i < numJoints; i++) {
          var jointNode = jointsArr[i];
          var jointMat = invMatArr[i] * inTransform * jointNode.GetMatrix();
          skin.OutputNodeMatrices[i] = jointMat;
        }
        skin.JointsCount = numJoints;
      }
    }

    foreach (var child in Children) {
      child.Update();
    }
  }

  public void CalculateMeshCenter() {
    if (!HasMesh) return;

    var sum = Vector3.Zero;
    Mesh? mesh = null;
    try {
      _app.Meshes.TryGetValue(MeshGuid, out mesh);
    } catch { }

    foreach (var vtx in mesh!.Vertices) {
      sum += vtx.Position;
    }

    Center = sum / mesh.VertexCount;
  }

  public bool HasMesh => MeshGuid != Guid.Empty;
  public bool HasSkin => SkinGuid != Guid.Empty;
  public void Dispose() {
    try {
      _app.Skins.TryGetValue(SkinGuid, out var skin);
      skin?.Dispose();
    } catch { }
    try {
      _app.Meshes.TryGetValue(MeshGuid, out var mesh); ;
    } catch { }
    GC.SuppressFinalize(this);
  }

  public Node CloneNode(Node parent) {
    var clone = (Node)Clone();
    clone.Parent = parent;
    return clone;
  }

  public object Clone() {
    var clone = new Node(_app, default) {
      Parent = null,
      Index = Index,
      NodeMatrix = NodeMatrix,
      Name = Name,
      // Mesh = (Mesh)Mesh?.Clone()! ?? null!,
      // Skin = (Skin)Skin?.Clone()! ?? null!,
      // Skin = Skin,
      // SkinIndex = SkinIndex,
      Translation = Translation,
      Rotation = Rotation,
      Scale = Scale,
      TranslationOffset = TranslationOffset,
      RotationOffset = RotationOffset,
      ScaleOffset = ScaleOffset,
      UseCachedMatrix = false,
      CachedLocalMatrix = Matrix4x4.Identity,
      CachedMatrix = Matrix4x4.Identity,
      BoundingVolume = BoundingVolume,
      AABB = AABB,
      Enabled = Enabled,
      AnimationTimer = 0.0f
    };

    clone.Children = Children.Select(child => {
      var childClone = child.CloneNode(clone);
      childClone.Parent = clone;
      return childClone;
    }).ToList();

    return clone;
  }

  public int CompareTo(Node? other) {
    if (other == null) return 1;
    // if (!other.HasMesh) return 1;

    return string.Compare(Name, other.Name, StringComparison.Ordinal);
    // int vertexComp = Mesh!.VertexCount.CompareTo(other.Mesh!.VertexCount);
    // return vertexComp;
  }
}