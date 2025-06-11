using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Dwarf.AbstractionLayer;
using Dwarf.EntityComponentSystem;
using Dwarf.EntityComponentSystem.Lightning;
using Dwarf.Utils;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Rendering.Lightning;

public class PointLightSystem : SystemBase {
  private Entity[] _lightsCache = [];
  private readonly unsafe PointLightPushConstant* _lightPushConstant =
    (PointLightPushConstant*)Marshal.AllocHGlobal(Unsafe.SizeOf<PointLightPushConstant>());

  public PointLightSystem(
    nint allocator,
    IDevice device,
    IRenderer renderer,
    IDescriptorSetLayout globalSetLayout,
    IPipelineConfigInfo configInfo = null!
  ) : base(allocator, device, renderer, configInfo) {
    IDescriptorSetLayout[] descriptorSetLayouts = [
      globalSetLayout,
    ];

    AddPipelineData<PointLightPushConstant>(new() {
      RenderPass = renderer.GetSwapchainRenderPass(),
      VertexName = "point_light_vertex",
      FragmentName = "point_light_fragment",
      PipelineProvider = new PipelinePointLightProvider(),
      DescriptorSetLayouts = descriptorSetLayouts,
    });
  }

  public void Setup() {
    _device.WaitQueue();
  }

  public unsafe void Update(ReadOnlySpan<Entity> entities, out PointLight[] lightData) {
    var lights = entities.DistinctReadOnlySpan<PointLightComponent>();

    if (lights.Length > 0) {
      _lightsCache = lights.ToArray();
    } else {
      Array.Clear(_lightsCache);
      lightData = [];
      return;
    }

    lightData = new PointLight[lights.Length];

    for (int i = 0; i < lights.Length; i++) {
      var pos = lights[i].GetComponent<Transform>();
      lightData[i].LightPosition = new Vector4(pos.Position, 1.0f);
      lightData[i].LightColor = lights[i].GetComponent<PointLightComponent>().Color;
    }
  }

  public void Render(FrameInfo frameInfo) {
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

    for (int i = 0; i < _lightsCache.Length; i++) {
      var light = _lightsCache[i].GetComponent<PointLightComponent>();
      var pos = _lightsCache[i].GetComponent<Transform>();
      unsafe {
        _lightPushConstant->Color = light.Color;
        _lightPushConstant->Position = new Vector4(pos.Position, 1.0f);
        _lightPushConstant->Radius = pos.Scale.X / 10;

        vkCmdPushConstants(
          frameInfo.CommandBuffer,
          PipelineLayout,
          VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment,
          0,
          (uint)Unsafe.SizeOf<PointLightPushConstant>(),
          _lightPushConstant
        );

        vkCmdDraw(frameInfo.CommandBuffer, 6, 1, 0, 0);
      }
    }
  }

  public override unsafe void Dispose() {
    _device.WaitQueue();
    _device.WaitDevice();

    MemoryUtils.FreeIntPtr<PointLightPushConstant>((nint)_lightPushConstant);

    base.Dispose();
  }
}