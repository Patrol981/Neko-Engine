namespace Neko.AbstractionLayer;

// TODO: Precompiler flags for other API
public enum DescriptorType {
  /// <unmanaged>VK_DESCRIPTOR_TYPE_SAMPLER</unmanaged>
  Sampler = 0,
  /// <unmanaged>VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER</unmanaged>
  CombinedImageSampler = 1,
  /// <unmanaged>VK_DESCRIPTOR_TYPE_SAMPLED_IMAGE</unmanaged>
  SampledImage = 2,
  /// <unmanaged>VK_DESCRIPTOR_TYPE_STORAGE_IMAGE</unmanaged>
  StorageImage = 3,
  /// <unmanaged>VK_DESCRIPTOR_TYPE_UNIFORM_TEXEL_BUFFER</unmanaged>
  UniformTexelBuffer = 4,
  /// <unmanaged>VK_DESCRIPTOR_TYPE_STORAGE_TEXEL_BUFFER</unmanaged>
  StorageTexelBuffer = 5,
  /// <unmanaged>VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER</unmanaged>
  UniformBuffer = 6,
  /// <unmanaged>VK_DESCRIPTOR_TYPE_STORAGE_BUFFER</unmanaged>
  StorageBuffer = 7,
  /// <unmanaged>VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER_DYNAMIC</unmanaged>
  UniformBufferDynamic = 8,
  /// <unmanaged>VK_DESCRIPTOR_TYPE_STORAGE_BUFFER_DYNAMIC</unmanaged>
  StorageBufferDynamic = 9,
  /// <unmanaged>VK_DESCRIPTOR_TYPE_INPUT_ATTACHMENT</unmanaged>
  InputAttachment = 10,
  /// <unmanaged>VK_DESCRIPTOR_TYPE_INLINE_UNIFORM_BLOCK</unmanaged>
  InlineUniformBlock = 1000138000,
  /// <unmanaged>VK_DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE_KHR</unmanaged>
  AccelerationStructureKHR = 1000150000,
  /// <unmanaged>VK_DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE_NV</unmanaged>
  AccelerationStructureNV = 1000165000,
  /// <unmanaged>VK_DESCRIPTOR_TYPE_SAMPLE_WEIGHT_IMAGE_QCOM</unmanaged>
  SampleWeightImageQCOM = 1000440000,
  /// <unmanaged>VK_DESCRIPTOR_TYPE_BLOCK_MATCH_IMAGE_QCOM</unmanaged>
  BlockMatchImageQCOM = 1000440001,
  /// <unmanaged>VK_DESCRIPTOR_TYPE_MUTABLE_EXT</unmanaged>
  MutableEXT = 1000351000,
  /// <unmanaged>VK_DESCRIPTOR_TYPE_MUTABLE_VALVE</unmanaged>
  MutableVALVE = MutableEXT,
}