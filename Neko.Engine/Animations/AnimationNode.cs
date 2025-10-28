using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.InteropServices;
using Neko.Rendering;
using Neko.Rendering.Renderer3D;
using Neko.Rendering.Renderer3D.Animations;

namespace Neko.Animations;

public unsafe struct AnimationNode {
  public int Index;
  public string Name;

  public int SkinIndex; // Index of skin in gltf model

  public Guid MeshId; // Id of mesh in Mesh table
  public Guid SkinId; // Id of skin in Skin table
  public MeshRenderer* MeshRenderer; // Id of MeshRenderer in MeshRenderer table

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
    SkinIndex = -1;
    Name = CommonConstants.NODE_INIT_NAME_NOT_SET;
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
      Update(&node->Children[i], ref meshTable, ref skinTable);
    }
  }

  public static unsafe void PushChildren(
    AnimationNode* target,
    AnimationNode* child
  ) {
    if (target == null) throw new ArgumentNullException(nameof(target));
    if (child == null) throw new ArgumentNullException(nameof(child));

    nuint oldCount = target->ChildrenCount;
    nuint newCount = oldCount + 1u;
    nuint newSize = (nuint)sizeof(AnimationNode) * newCount;

    // Reallocates the buffer (works with null -> behaves like Alloc)
    target->Children = (AnimationNode*)NativeMemory.Realloc(target->Children, newSize);
    if (target->Children == null)
      throw new OutOfMemoryException("Failed to grow children buffer.");

    // parent link + copy the struct into the last slot
    child->Parent = target;
    target->Children[oldCount] = *child;

    target->ChildrenCount = (uint)newCount;
  }

  public static void SetMeshRenderer(AnimationNode* node, MeshRenderer* meshRenderer) {
    node->MeshRenderer = meshRenderer;
  }

  public static unsafe void FreeChildrenRecursive(AnimationNode* node) {
    for (uint i = 0; i < node->ChildrenCount; i++)
      FreeChildrenRecursive(&node->Children[i]);

    NativeMemory.Free(node->Children);
    node->Children = null;
    node->ChildrenCount = 0;
  }

  public static unsafe void Dispose(AnimationNode* node) {
    FreeChildrenRecursive(node);
  }
}