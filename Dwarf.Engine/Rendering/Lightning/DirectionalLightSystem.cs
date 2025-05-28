using Dwarf.AbstractionLayer;
using Dwarf.Globals;
using Dwarf.Rendering.Lightning;
using Dwarf.Vulkan;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Rendering.Lightning;

public class DirectionalLightSystem : SystemBase {
  public DirectionalLightSystem(
    nint allocator,
    IDevice device,
    IRenderer renderer,
    IDescriptorSetLayout globalSetLayout,
    IPipelineConfigInfo configInfo = null!
  ) : base(allocator, device, renderer, configInfo) {
    IDescriptorSetLayout[] descriptorSetLayouts = [
      globalSetLayout,
    ];

    AddPipelineData(new() {
      RenderPass = VkRenderPass.Null,
      VertexName = "directional_light_vertex",
      FragmentName = "directional_light_fragment",
      PipelineProvider = new PipelinePointLightProvider(),
      DescriptorSetLayouts = descriptorSetLayouts,
    });
  }

  public void Setup() {
    _device.WaitQueue();
  }

  public void Render(FrameInfo frameInfo) {
    if (!PerfMonitor.IsDebug) return;

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

    vkCmdDraw(frameInfo.CommandBuffer, 6, 1, 0, 0);
  }

  public override unsafe void Dispose() {
    _device.WaitQueue();
    _device.WaitDevice();

    base.Dispose();
  }
}
