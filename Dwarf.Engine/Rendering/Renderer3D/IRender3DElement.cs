using System.Numerics;

using Dwarf.AbstractionLayer;
using Dwarf.EntityComponentSystem;
using Dwarf.Math;
using Dwarf.Vulkan;

using Vortice.Vulkan;

namespace Dwarf.Rendering.Renderer3D;

public interface IRender3DElement : IDrawable {
  int NodesCount { get; }
  int MeshedNodesCount { get; }
  int LinearNodesCount { get; }
  Node[] Nodes { get; }
  Node[] MeshedNodes { get; }
  Node[] LinearNodes { get; }
  bool FinishedInitialization { get; }
  bool IsSkinned { get; }
  ulong CalculateBufferSize();
  DwarfBuffer Ssbo { get; }
  Matrix4x4[] InverseMatrices { get; }
  VkDescriptorSet SkinDescriptor { get; }
  void BuildDescriptors(IDescriptorSetLayout descriptorSetLayout, IDescriptorPool descriptorPool);
  Entity Owner { get; }
  Guid GetTextureIdReference(int index = 0);
  float CalculateHeightOfAnModel();
  AABB AABB { get; }
  bool FilterMeInShader { get; }
  // public void DrawIndirect(VkCommandBuffer commandBuffer, VkBuffer buffer, ulong offset, uint drawCount, uint stride);
}
