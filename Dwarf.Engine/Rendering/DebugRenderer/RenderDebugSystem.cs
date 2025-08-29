using System.Numerics;
using System.Runtime.CompilerServices;
using Dwarf.AbstractionLayer;
using Dwarf.EntityComponentSystem;
using Dwarf.Globals;
using Dwarf.Physics;
using Dwarf.Physics.Interfaces;
using Dwarf.Rendering.Renderer3D;
using Dwarf.Vulkan;
using OpenTK.Mathematics;
using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Rendering.DebugRenderer;

public class RenderDebugSystem : SystemBase, IRenderSystem {
  public RenderDebugSystem(
    nint allocator,
    IDevice device,
    IRenderer renderer,
    TextureManager textureManager,
    IDescriptorSetLayout globalSetLayout,
    IPipelineConfigInfo configInfo = null!
  ) : base(allocator, device, renderer, textureManager, configInfo) {

    IDescriptorSetLayout[] descriptorSetLayouts = [
      globalSetLayout,
    ];

    AddPipelineData<ColliderMeshPushConstant>(new() {
      RenderPass = renderer.GetPostProcessingPass(),
      VertexName = "debug_vertex",
      FragmentName = "debug_fragment",
      PipelineProvider = new PipelineModelProvider(),
      DescriptorSetLayouts = descriptorSetLayouts,
    });
  }

  public unsafe void Render(FrameInfo frameInfo, ReadOnlySpan<ColliderMesh> colliderMeshes) {
    if (!PerfMonitor.IsDebug) return;

    BindPipeline(frameInfo.CommandBuffer);

    vkCmdBindDescriptorSets(
      frameInfo.CommandBuffer,
      VkPipelineBindPoint.Graphics,
      PipelineLayout,
      0,
      1,
      &frameInfo.GlobalDescriptorSet,
      0,
      null
    );

    for (int i = 0; i < colliderMeshes.Length; i++) {
      if (!colliderMeshes[i].Enabled || colliderMeshes[i].Owner.CanBeDisposed) continue;

      var pushConstant = new ColliderMeshPushConstant {
        ModelMatrix = colliderMeshes[i].Owner?.GetTransform()?.MatrixWithAngleYRotationWithoutScale() ?? Matrix4x4.Identity
      };

      vkCmdPushConstants(
        frameInfo.CommandBuffer,
        PipelineLayout,
        VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment,
        0,
        (uint)Unsafe.SizeOf<ColliderMeshPushConstant>(),
        &pushConstant
      );

      colliderMeshes[i].Bind(frameInfo.CommandBuffer, 0);

      for (uint x = 0; x < colliderMeshes[i].MeshsesCount; x++) {
        if (!colliderMeshes[i].FinishedInitialization) continue;
        colliderMeshes[i].Draw(frameInfo.CommandBuffer, x);
      }
    }
  }

  public override unsafe void Dispose() {
    base.Dispose();
  }

  public void Setup(ReadOnlySpan<Entity> entities, ref TextureManager textures) {
    throw new NotImplementedException();
  }
}
