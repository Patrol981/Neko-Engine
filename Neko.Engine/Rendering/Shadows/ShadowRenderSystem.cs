using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Neko.AbstractionLayer;
using Neko.EntityComponentSystem;
using Neko.Rendering.Renderer3D;
using Neko.Utils;
using Neko.Vulkan;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Neko.Rendering.Shadows;

public class ShadowRenderSystem : SystemBase {
  public readonly Application _application;

  private Mesh _shadowMesh = null!;
  private List<TransformComponent> _positions = [];
  private readonly unsafe ShadowPushConstant* _shadowPushConstant =
    (ShadowPushConstant*)Marshal.AllocHGlobal(Unsafe.SizeOf<ShadowPushConstant>());

  public ShadowRenderSystem(
    Application app,
    nint allocator,
    VulkanDevice device,
    IRenderer renderer,
    TextureManager textureManager,
    SystemConfiguration systemConfiguration,
    Dictionary<string, IDescriptorSetLayout> externalLayouts,
    IPipelineConfigInfo configInfo = null!
  ) : base(app, allocator, device, renderer, textureManager, configInfo) {
    _application = Application.Instance;

    IDescriptorSetLayout[] layouts = [
      externalLayouts["Global"],
    ];

    AddPipelineData<ShadowPushConstant>(new() {
      VertexName = "shadow_vertex",
      FragmentName = "shadow_fragment",
      PipelineProvider = new PipelineModelProvider(),
      DescriptorSetLayouts = layouts,
    });

    Setup();
  }

  public void Setup() {
    _shadowMesh = Primitives.CreatePlanePrimitive(1);
  }

  public void Update(Span<IRender3DElement> i3D) {
    _positions.Clear();
    for (int i = 0; i < i3D.Length; i++) {
      _positions.Add(i3D[i].Owner.GetTransform()!);
    }
  }

  public unsafe void Render(FrameInfo frameInfo) {
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

    // _renderer.CommandList.BindVertex(frameInfo.CommandBuffer, _shadowMesh.VertexBuffer!, 0);
    // if (_shadowMesh.HasIndexBuffer) {
    //   _renderer.CommandList.BindIndex(frameInfo.CommandBuffer, _shadowMesh.IndexBuffer!);
    // }

    for (int i = 0; i < _positions.Count; i++) {
      _shadowPushConstant->Transform = _positions[i].Position();
      _shadowPushConstant->Radius = 1;

      _device.DeviceApi.vkCmdPushConstants(
        frameInfo.CommandBuffer,
        PipelineLayout,
        VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment,
        0,
        (uint)Unsafe.SizeOf<ShadowPushConstant>(),
        _shadowPushConstant
      );


      if (_shadowMesh.HasIndexBuffer) {
        _renderer.CommandList.DrawIndexed(frameInfo.CommandBuffer, _shadowMesh.IndexCount, 1, 0, 0, 0);
      } else {
        _renderer.CommandList.Draw(frameInfo.CommandBuffer, _shadowMesh.VertexCount, 1, 0, 0);
      }
    }
  }

  public unsafe override void Dispose() {
    MemoryUtils.FreeIntPtr<ShadowPushConstant>((nint)_shadowPushConstant);
    base.Dispose();
  }
}