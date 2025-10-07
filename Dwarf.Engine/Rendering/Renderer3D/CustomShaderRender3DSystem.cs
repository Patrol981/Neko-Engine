using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Dwarf;
using Dwarf.AbstractionLayer;
using Dwarf.Rendering;
using Dwarf.Vulkan;

namespace Dwarf.Rendering.Renderer3D;

public class CustomShaderRender3DSystem : SystemBase, IRenderSystem {
  private readonly IDescriptorSetLayout[] _basicLayouts = [];

  public int LastKnownElemCount { get; set; } = 0;

  public CustomShaderRender3DSystem(
    Application app,
    nint allocator,
    VulkanDevice device,
    IRenderer renderer,
    TextureManager textureManager,
    Dictionary<string, IDescriptorSetLayout> externalLayouts,
    IPipelineConfigInfo configInfo = null!
  ) : base(app, allocator, device, renderer, textureManager, configInfo) {
    _basicLayouts = [
      _textureManager.AllTexturesSetLayout,
      externalLayouts["Global"],
      externalLayouts["CustomShaderObjectData"],
      externalLayouts["PointLight"],
    ];
  }

  public void Setup(ReadOnlySpan<IRender3DElement> renderablesWithCustomShaders) {


    foreach (var renderable in renderablesWithCustomShaders) {
      var pipelineName = renderable.CustomShader.Name;
      AddPipelineData(new() {
        RenderPass = _application.Renderer.GetSwapchainRenderPass(),
        VertexName = "model_vertex",
        FragmentName = $"{pipelineName}_fragment",
        GeometryName = "model_geometry",
        PipelineProvider = new PipelineModelProvider(),
        DescriptorSetLayouts = [.. _basicLayouts],
        PipelineName = pipelineName
      });
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveOptimization)]
  public void Update(FrameInfo frameInfo, ConcurrentDictionary<Guid, Mesh> meshes) {

  }

  public void Render() {

  }

  public override void Dispose() {
    base.Dispose();
  }
}