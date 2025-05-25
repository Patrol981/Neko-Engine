using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Dwarf.AbstractionLayer;
using Dwarf.Globals;
using Dwarf.Utils;
using Dwarf.Vulkan;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Rendering.Guizmos;

public struct GuizmoIndirectBatch {
  public uint First;
  public uint Count;
};

public class GuizmoRenderSystem : SystemBase {
  private readonly unsafe GuizmoBufferObject* _bufferObject =
    (GuizmoBufferObject*)Marshal.AllocHGlobal(Unsafe.SizeOf<GuizmoBufferObject>());

  public GuizmoRenderSystem(
    nint allocator,
    IDevice device,
    IRenderer renderer,
    IDescriptorSetLayout globalSetLayout,
    PipelineConfigInfo configInfo = null!
  ) : base(allocator, device, renderer, configInfo) {
    VkDescriptorSetLayout[] descriptorSetLayouts = [
      globalSetLayout.GetDescriptorSetLayoutPointer(),
    ];

    AddPipelineData<GuizmoBufferObject>(new() {
      RenderPass = renderer.GetSwapchainRenderPass(),
      VertexName = "guizmo_vertex",
      FragmentName = "guizmo_fragment",
      PipelineProvider = new GuizmoPipelineProvider(),
      DescriptorSetLayouts = descriptorSetLayouts,
    });
  }

  public void Render(FrameInfo frameInfo) {
    if (Globals.Guizmos.Data.Count < 1) return;

    BindPipeline(frameInfo.CommandBuffer);
    unsafe {
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
    }

    var guizmos = Globals.Guizmos.Data;
    var perFrameGuizmos = Globals.Guizmos.PerFrameGuizmos;

    Draw(frameInfo, guizmos);

    if (!perFrameGuizmos.IsEmpty && perFrameGuizmos.Length > 0) {
      // Draw(frameInfo, perFrameGuizmos);
      // Guizmos.Free();
    }
  }

  private void Draw(FrameInfo frameInfo, List<Guizmo> guizmos) {
    var tmp = guizmos.ToArray().Clone() as Guizmo[];
    for (int i = 0; i < tmp?.Length; i++) {
      unsafe {
        var color = tmp[i]?.Color ?? Vector3.One;
        _bufferObject->ModelMatrix = tmp[i]?.Transform.Matrix4 ?? Matrix4x4.Identity;
        _bufferObject->GuizmoType = (int)GuizmoType.Circular;
        _bufferObject->ColorX = color.X;
        _bufferObject->ColorY = color.Y;
        _bufferObject->ColorZ = color.Z;

        vkCmdPushConstants(
          frameInfo.CommandBuffer,
          PipelineLayout,
          VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment,
          0,
          (uint)Unsafe.SizeOf<GuizmoBufferObject>(),
          _bufferObject
        );
      }

      vkCmdDraw(frameInfo.CommandBuffer, 6, 1, 0, 0);
    }
  }

  public override unsafe void Dispose() {
    MemoryUtils.FreeIntPtr<GuizmoBufferObject>((nint)_bufferObject);

    base.Dispose();
  }
}
