using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Dwarf.AbstractionLayer;
using Dwarf.EntityComponentSystem;
using Dwarf.Utils;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Rendering.Lightning;

public class PointLightSystem : SystemBase {
  private PointLightComponent[] _lightsCache = [];
  private readonly unsafe PointLightPushConstant* _lightPushConstant =
    (PointLightPushConstant*)Marshal.AllocHGlobal(Unsafe.SizeOf<PointLightPushConstant>());

  public PointLightSystem(
    Application app,
    nint allocator,
    IDevice device,
    IRenderer renderer,
    TextureManager textureManager,
    IDescriptorSetLayout globalSetLayout,
    IPipelineConfigInfo configInfo = null!
  ) : base(app, allocator, device, renderer, textureManager, configInfo) {
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

  public unsafe void Update(ReadOnlySpan<PointLightComponent> lights, out PointLight[] lightData) {
    if (lights.Length > 0) {
      _lightsCache = lights.ToArray();
    } else {
      Array.Clear(_lightsCache);
      lightData = [];
      return;
    }

    lightData = new PointLight[lights.Length];

    for (int i = 0; i < lights.Length; i++) {
      var pos = lights[i].Owner.GetTransform();
      lightData[i].LightPosition = new Vector4(pos!.Position, 1.0f);
      lightData[i].LightColor = lights[i].Color;
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
      var pos = _lightsCache[i].Owner.GetTransform();
      unsafe {
        _lightPushConstant->Color = _lightsCache[i].Color;
        _lightPushConstant->Position = new Vector4(pos!.Position, 1.0f);
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