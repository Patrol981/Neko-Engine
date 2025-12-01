using Neko.EntityComponentSystem;
using Vortice.Vulkan;

namespace Neko;

public struct FrameInfo {
  public int FrameIndex;
  public VkCommandBuffer CommandBuffer;
  public Camera Camera;
  public VkDescriptorSet GlobalDescriptorSet;
  public VkDescriptorSet PointLightsDescriptorSet;
  [Obsolete] public VkDescriptorSet ObjectDataDescriptorSet;
  public VkDescriptorSet StaticObjectDataDescriptorSet;
  public VkDescriptorSet SkinnedObjectDataDescriptorSet;
  public VkDescriptorSet CustomShaderObjectDataDescriptorSet;
  public VkDescriptorSet JointsBufferDescriptorSet;
  public VkDescriptorSet SpriteDataDescriptorSet;
  public VkDescriptorSet TileLayerDataDescriptorSet;
  public VkDescriptorSet CustomSpriteDataDescriptorSet;
  public TextureManager TextureManager;
  public Entity ImportantEntity;
}