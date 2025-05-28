using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Dwarf.AbstractionLayer;
using Dwarf.Rendering.Renderer3D;
using Dwarf.Utils;
using Dwarf.Vulkan;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Rendering.Shadows;

public class ShadowRenderSystem : SystemBase {
  public readonly Application _application;

  private Mesh _shadowMesh = null!;
  private List<Transform> _positions = [];
  private readonly unsafe ShadowPushConstant* _shadowPushConstant =
    (ShadowPushConstant*)Marshal.AllocHGlobal(Unsafe.SizeOf<ShadowPushConstant>());

  public ShadowRenderSystem(
    nint allocator,
    IDevice device,
    IRenderer renderer,
    SystemConfiguration systemConfiguration,
    Dictionary<string, IDescriptorSetLayout> externalLayouts,
    IPipelineConfigInfo configInfo = null!
  ) : base(allocator, device, renderer, configInfo) {
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
    _shadowMesh.CreateVertexBuffer();
    _shadowMesh.CreateIndexBuffer();
  }

  public void Update(Span<IRender3DElement> i3D) {
    _positions.Clear();
    for (int i = 0; i < i3D.Length; i++) {
      _positions.Add(i3D[i].GetOwner().GetComponent<Transform>());
    }
  }

  public unsafe void Render(FrameInfo frameInfo) {
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

    _renderer.CommandList.BindVertex(frameInfo.CommandBuffer, _shadowMesh.VertexBuffer!, 0);
    if (_shadowMesh.HasIndexBuffer) {
      _renderer.CommandList.BindIndex(frameInfo.CommandBuffer, _shadowMesh.IndexBuffer!);
    }

    for (int i = 0; i < _positions.Count; i++) {
      _shadowPushConstant->Transform = _positions[i].PositionMatrix;
      _shadowPushConstant->Radius = 1;

      vkCmdPushConstants(
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