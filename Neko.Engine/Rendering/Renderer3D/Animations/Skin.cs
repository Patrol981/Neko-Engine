using System.Numerics;

namespace Neko.Rendering.Renderer3D.Animations;

public class Skin : IDisposable, ICloneable {
  public string Name { get; set; } = default!;

  public Node SkeletonRoot = null!;
  public List<Matrix4x4> InverseBindMatrices = null!;
  public List<Node> Joints = [];

  public Matrix4x4[] OutputNodeMatrices = [];
  public int JointsCount;

  public Skin() {
  }

  public void Init() {
    OutputNodeMatrices = new Matrix4x4[Joints.Count];
    for (int i = 0; i < OutputNodeMatrices.Length; i++) {
      OutputNodeMatrices[i] = Matrix4x4.Identity;
    }
  }
  public void Dispose() {
  }

  public object Clone() {
    var clone = new Skin();
    if (SkeletonRoot != null) {
      clone.SkeletonRoot = (Node)SkeletonRoot.Clone();
    }
    if (InverseBindMatrices != null) {
      clone.InverseBindMatrices = InverseBindMatrices;
    }
    if (Joints != null) {
      clone.Joints = Joints.Select(x => (Node)x.Clone()).ToList();
    }

    clone.JointsCount = JointsCount;

    return clone;
  }
}
