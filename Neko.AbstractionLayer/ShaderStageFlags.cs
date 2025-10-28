namespace Neko.AbstractionLayer;

[Flags]
public enum ShaderStageFlags {
  None = 0,
  /// <unmanaged>VK_SHADER_STAGE_VERTEX_BIT</unmanaged>
  Vertex = 0x00000001,
  /// <unmanaged>VK_SHADER_STAGE_TESSELLATION_CONTROL_BIT</unmanaged>
  TessellationControl = 0x00000002,
  /// <unmanaged>VK_SHADER_STAGE_TESSELLATION_EVALUATION_BIT</unmanaged>
  TessellationEvaluation = 0x00000004,
  /// <unmanaged>VK_SHADER_STAGE_GEOMETRY_BIT</unmanaged>
  Geometry = 0x00000008,
  /// <unmanaged>VK_SHADER_STAGE_FRAGMENT_BIT</unmanaged>
  Fragment = 0x00000010,
  /// <unmanaged>VK_SHADER_STAGE_COMPUTE_BIT</unmanaged>
  Compute = 0x00000020,
  /// <unmanaged>VK_SHADER_STAGE_ALL_GRAPHICS</unmanaged>
  AllGraphics = 0x0000001F,
  /// <unmanaged>VK_SHADER_STAGE_ALL</unmanaged>
  All = 0x7FFFFFFF,
  /// <unmanaged>VK_SHADER_STAGE_RAYGEN_BIT_KHR</unmanaged>
  RaygenKHR = 0x00000100,
  /// <unmanaged>VK_SHADER_STAGE_ANY_HIT_BIT_KHR</unmanaged>
  AnyHitKHR = 0x00000200,
  /// <unmanaged>VK_SHADER_STAGE_CLOSEST_HIT_BIT_KHR</unmanaged>
  ClosestHitKHR = 0x00000400,
  /// <unmanaged>VK_SHADER_STAGE_MISS_BIT_KHR</unmanaged>
  MissKHR = 0x00000800,
  /// <unmanaged>VK_SHADER_STAGE_INTERSECTION_BIT_KHR</unmanaged>
  IntersectionKHR = 0x00001000,
  /// <unmanaged>VK_SHADER_STAGE_CALLABLE_BIT_KHR</unmanaged>
  CallableKHR = 0x00002000,
  /// <unmanaged>VK_SHADER_STAGE_TASK_BIT_EXT</unmanaged>
  TaskEXT = 0x00000040,
  /// <unmanaged>VK_SHADER_STAGE_MESH_BIT_EXT</unmanaged>
  MeshEXT = 0x00000080,
  /// <unmanaged>VK_SHADER_STAGE_SUBPASS_SHADING_BIT_HUAWEI</unmanaged>
  SubpassShadingHUAWEI = 0x00004000,
  /// <unmanaged>VK_SHADER_STAGE_CLUSTER_CULLING_BIT_HUAWEI</unmanaged>
  ClusterCullingHUAWEI = 0x00080000,
}