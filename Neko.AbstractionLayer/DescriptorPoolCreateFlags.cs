namespace Neko.AbstractionLayer;

[Flags]
public enum DescriptorPoolCreateFlags {
  None = 0,
  /// <summary>
  /// Descriptor sets may be freed individually
  /// </summary>
  /// <unmanaged>VK_DESCRIPTOR_POOL_CREATE_FREE_DESCRIPTOR_SET_BIT</unmanaged>
  FreeDescriptorSet = 0x00000001,
  /// <unmanaged>VK_DESCRIPTOR_POOL_CREATE_UPDATE_AFTER_BIND_BIT</unmanaged>
  UpdateAfterBind = 0x00000002,
  /// <unmanaged>VK_DESCRIPTOR_POOL_CREATE_HOST_ONLY_BIT_EXT</unmanaged>
  HostOnlyEXT = 0x00000004,
  /// <unmanaged>VK_DESCRIPTOR_POOL_CREATE_ALLOW_OVERALLOCATION_SETS_BIT_NV</unmanaged>
  AllowOverallocationSetsNV = 0x00000008,
  /// <unmanaged>VK_DESCRIPTOR_POOL_CREATE_ALLOW_OVERALLOCATION_POOLS_BIT_NV</unmanaged>
  AllowOverallocationPoolsNV = 0x00000010,
  /// <unmanaged>VK_DESCRIPTOR_POOL_CREATE_HOST_ONLY_BIT_VALVE</unmanaged>
  HostOnlyVALVE = HostOnlyEXT,
}