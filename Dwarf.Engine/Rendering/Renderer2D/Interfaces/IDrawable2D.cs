using Dwarf.AbstractionLayer;
using Dwarf.EntityComponentSystem;
using Dwarf.Math;
using Dwarf.Vulkan;

namespace Dwarf.Rendering.Renderer2D.Interfaces;

public interface IDrawable2D : IDrawable {
  void BuildDescriptors(IDescriptorSetLayout descriptorSetLayout, IDescriptorPool descriptorPool);
  void CachePipelineLayout(object pipelineLayout);

  bool DescriptorBuilt { get; }

  Entity Entity { get; }
  bool Active { get; }
  ITexture Texture { get; }
  Vector2I SpriteSheetSize { get; }
  int SpriteIndex { get; }
  int SpriteCount { get; }
  bool FlipX { get; set; }
  bool FlipY { get; set; }
  bool NeedPipelineCache { get; }
  Mesh CollisionMesh { get; }
}