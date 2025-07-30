using System.Numerics;
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
  public const int MAX_NUM_JOINTS = 128;

  public Node? Parent;
  public int Index = 0;
  public List<Node> Children = [];
  public Matrix4x4 NodeMatrix = Matrix4x4.Identity;
  public string Name = string.Empty;
  public Mesh? Mesh;
  public Skin? Skin;
  public int SkinIndex = -1;
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

  public Matrix4x4 GetLocalMatrix() {
    if (!UseCachedMatrix) {
      CachedLocalMatrix =
        NodeMatrix *
        Matrix4x4.CreateScale(Scale) *
        Matrix4x4.CreateFromQuaternion(Rotation) *
        Matrix4x4.CreateTranslation(Translation);
    }
    return CachedLocalMatrix;
  }

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

  public void DrawNode(IntPtr commandBuffer, int vertexOffset, uint firstInstance = 0) {
    if (!HasMesh || !Enabled) return;

    if (Mesh!.HasIndexBuffer) {
      ParentRenderer.Renderer.CommandList.DrawIndexed(commandBuffer, Mesh!.IndexCount, 1, 0, vertexOffset, firstInstance);
    } else {
      ParentRenderer.Renderer.CommandList.Draw(commandBuffer, Mesh!.VertexCount, 1, 0, firstInstance);
    }
  }

  public void DrawNode(IntPtr commandBuffer, uint firstInstance = 0) {
    if (!HasMesh || !Enabled) return;

    if (Mesh!.HasIndexBuffer) {
      ParentRenderer.Renderer.CommandList.DrawIndexed(commandBuffer, Mesh!.IndexCount, 1, 0, 0, firstInstance);
    } else {
      ParentRenderer.Renderer.CommandList.Draw(commandBuffer, Mesh!.VertexCount, 1, 0, firstInstance);
    }
  }

  public void BindNode(IntPtr commandBuffer) {
    if (!HasMesh || !Enabled) return;

    ParentRenderer.Renderer.CommandList.BindVertex(commandBuffer, Mesh!.VertexBuffer!, 0);

    if (Mesh!.HasIndexBuffer) {
      ParentRenderer.Renderer.CommandList.BindIndex(commandBuffer, Mesh!.IndexBuffer!);
    }
  }

  public void BindNode(IntPtr commandBuffer, DwarfBuffer vertexBuffer, DwarfBuffer indexBuffer, ulong vertexOffset, ulong indexOffset) {
    if (!HasMesh || !Enabled) return;

    ParentRenderer.Renderer.CommandList.BindVertex(commandBuffer, vertexBuffer, vertexOffset);

    if (Mesh!.HasIndexBuffer) {
      ParentRenderer.Renderer.CommandList.BindIndex(commandBuffer, indexBuffer, indexOffset);
    }
  }

  public void Update() {
    UseCachedMatrix = false;
    if (Mesh != null) {
      Matrix4x4 m = GetMatrix();
      if (Skin != null) {
        Mesh.Matrix = m;
        Matrix4x4.Invert(m, out var inTransform);
        int numJoints = (int)MathF.Min(Skin.Joints.Count, MAX_NUM_JOINTS);
        for (int i = 0; i < numJoints; i++) {
          var jointNode = Skin.Joints[i];
          var jointMat = Skin.InverseBindMatrices[i] * inTransform * jointNode.GetMatrix();
          Skin.OutputNodeMatrices[i] = jointMat;
        }
        Skin.JointsCount = numJoints;
      } else {
        Mesh.Matrix = m;
      }
    }

    foreach (var child in Children) {
      child.Update();
    }
  }

  public void CalculateMeshCenter() {
    if (!HasMesh) return;

    var sum = Vector3.Zero;

    foreach (var vtx in Mesh!.Vertices) {
      sum += vtx.Position;
    }

    Center = sum / Mesh.VertexCount;
  }

  public bool HasMesh => Mesh != null;
  public bool HasSkin => Skin != null;
  public void Dispose() {
    Skin?.Dispose();
    Mesh?.Dispose();
    GC.SuppressFinalize(this);
  }

  public Node CloneNode(Node parent) {
    var clone = (Node)Clone();
    clone.Parent = parent;
    return clone;
  }

  public object Clone() {
    var clone = new Node {
      Parent = null,
      Index = Index,
      NodeMatrix = NodeMatrix,
      Name = Name,
      Mesh = (Mesh)Mesh?.Clone()! ?? null!,
      // Skin = (Skin)Skin?.Clone()! ?? null!,
      // Skin = Skin,
      SkinIndex = SkinIndex,
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