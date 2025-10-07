using System.Collections.Concurrent;
using System.Numerics;

using Dwarf.AbstractionLayer;
using Dwarf.EntityComponentSystem;
using Dwarf.Math;
using Dwarf.Rendering.Renderer3D.Animations;
using Dwarf.Vulkan;

using Vortice.Vulkan;

namespace Dwarf.Rendering.Renderer3D;

public interface IRender3DElement : IDisposable {
  void AddLinearNode(Node node);
  void BindToTexture(
    TextureManager textureManager,
    string texturePath,
    in ConcurrentDictionary<Guid, Mesh> meshes,
    int modelPart = 0
  );
  void Init(in ConcurrentDictionary<Guid, Mesh> meshes, AABBFilter aabbFilter = AABBFilter.None);
  void EnableNode(Func<Node, bool> predicate, bool enabled);

  ShaderInfo CustomShader { get; }
  void SetCustomShader(ShaderInfo shaderInfo);

  int NodesCount { get; }
  int MeshedNodesCount { get; }
  int LinearNodesCount { get; }
  Node[] Nodes { get; }
  Node[] MeshedNodes { get; }
  Node[] LinearNodes { get; }
  List<Animation> Animations { get; }
  bool FinishedInitialization { get; }
  bool IsSkinned { get; }
  ulong CalculateBufferSize();
  DwarfBuffer Ssbo { get; }
  Matrix4x4[] InverseMatrices { get; }
  VkDescriptorSet SkinDescriptor { get; }
  void BuildDescriptors(IDescriptorSetLayout descriptorSetLayout, IDescriptorPool descriptorPool);
  Entity Owner { get; }
  Guid GetTextureIdReference(in ConcurrentDictionary<Guid, Mesh> meshes, int index = 0);
  float CalculateHeightOfAnModel(in ConcurrentDictionary<Guid, Mesh> meshes);
  AABB AABB { get; }
  AABBFilter AABBFilter { get; }
  bool FilterMeInShader { get; }
  float Radius { get; }
  // public void DrawIndirect(VkCommandBuffer commandBuffer, VkBuffer buffer, ulong offset, uint drawCount, uint stride);
}
