using Neko.AbstractionLayer;
using Neko.Globals;
using Neko.Rendering.Lightning;
using Neko.Vulkan;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Neko.Rendering.Lightning;

public class DirectionalLightSystem : SystemBase {
  public DirectionalLightSystem(
    Application app,
    nint allocator,
    VulkanDevice device,
    IRenderer renderer,
    TextureManager textureManager,
    IDescriptorSetLayout globalSetLayout,
    IPipelineConfigInfo configInfo = null!
  ) : base(app, allocator, device, renderer, textureManager, configInfo) {
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
      _device.DeviceApi.vkCmdBindDescriptorSets(
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

    _device.DeviceApi.vkCmdDraw(frameInfo.CommandBuffer, 6, 1, 0, 0);
  }

  public override unsafe void Dispose() {
    _device.WaitQueue();
    _device.WaitDevice();

    base.Dispose();
  }
}
