using System.Collections.Concurrent;
using System.Numerics;
using Dwarf.Rendering;
using Dwarf.Rendering.Renderer3D.Animations;

namespace Dwarf.Animations;

public unsafe struct AnimationNode {
  public int Index;
  public Guid MeshId; // Id of mesh in Mesh table
  public Guid SkinId; // Id of skin in Skin table

  public Vector3 Translation;
  public Quaternion Rotation;
  public Vector3 Scale;
  public Vector3 TranslationOffset;
  public Quaternion RotationOffset;
  public Vector3 ScaleOffset;

  public Matrix4x4 NodeMatrix;
  public Matrix4x4 CachedLocalMatrix;
  public Matrix4x4 CachedMatrix;

  public AnimationNode* Parent;
  public AnimationNode* Children;
  public uint ChildrenCount;

  public bool UseCachedMatrix;
  public bool Enabled;

  public AnimationNode() {
    MeshId = Guid.Empty;
    SkinId = Guid.Empty;
  }

  public static unsafe Matrix4x4 GetLocalMatrix(AnimationNode* node) {
    if (node->UseCachedMatrix) {
      node->CachedLocalMatrix =
        node->NodeMatrix *
        Matrix4x4.CreateScale(node->Scale) *
        Matrix4x4.CreateFromQuaternion(node->Rotation) *
        Matrix4x4.CreateTranslation(node->Translation);
    }
    return node->CachedLocalMatrix;
  }

  public static unsafe Matrix4x4 GetMatrix(AnimationNode* node) {
    if (node->UseCachedMatrix) {
      var m = GetLocalMatrix(node);
      var p = node->Parent;
      while (p != null) {
        m *= GetLocalMatrix(p);
        p = p->Parent;
      }
      node->CachedLocalMatrix = m;
      node->UseCachedMatrix = true;
      return m;
    } else {
      return node->CachedLocalMatrix;
    }
  }

  public static unsafe void Update(
    AnimationNode* node,
    ref ConcurrentDictionary<Guid, Mesh> meshTable,
    ref ConcurrentDictionary<Guid, Skin> skinTable
  ) {
    node->UseCachedMatrix = false;
    if (node->MeshId != Guid.Empty) {
      var m = GetMatrix(node);
      if (node->SkinId != Guid.Empty) {
        meshTable[node->MeshId].Matrix = m;
        Matrix4x4.Invert(m, out var inTransform);
        int numJoints = (int)MathF.Min(skinTable[node->SkinId].Joints.Count, CommonConstants.MAX_NUM_JOINTS);
        for (short i = 0; i < numJoints; i++) {
          var joinNode = skinTable[node->SkinId].Joints[i];
          var jointMat = skinTable[node->SkinId].InverseBindMatrices[i] * inTransform * joinNode.GetMatrix();
          skinTable[node->SkinId].OutputNodeMatrices[i] = jointMat;
        }
        skinTable[node->SkinId].JointsCount = numJoints;
      } else {
        meshTable[node->MeshId].Matrix = m;
      }
    }

    for (short i = 0; i < node->ChildrenCount; i++) {
      var child = node->Children[i];
      Update(&child, ref meshTable, ref skinTable);
    }
  }
}