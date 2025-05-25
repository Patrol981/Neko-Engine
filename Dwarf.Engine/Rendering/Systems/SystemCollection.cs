using Dwarf.AbstractionLayer;
using Dwarf.EntityComponentSystem;
using Dwarf.Networking;
using Dwarf.Physics;
using Dwarf.Physics.Interfaces;
using Dwarf.Rendering.DebugRenderer;
using Dwarf.Rendering.Guizmos;
using Dwarf.Rendering.Lightning;
using Dwarf.Rendering.Particles;
using Dwarf.Rendering.PostProcessing;
using Dwarf.Rendering.Renderer2D;
using Dwarf.Rendering.Renderer3D;
using Dwarf.Rendering.Shadows;
using Dwarf.Vulkan;

using Vortice.Vulkan;

namespace Dwarf.Rendering;

public class SystemCollection : IDisposable {
  // Render Systems
  private Render3DSystem? _render3DSystem;
  private Render2DSystem? _render2DSystem;
  private RenderUISystem? _renderUISystem;
  private RenderDebugSystem? _renderDebugSystem;
  private DirectionalLightSystem? _directionaLightSystem;
  private PointLightSystem? _pointLightSystem;
  private GuizmoRenderSystem? _guizmoRenderSystem;
  private ParticleSystem? _particleSystem;
  private ShadowRenderSystem? _shadowRenderSystem;

  private PostProcessingSystem? _postProcessingSystem;

  // Calculation Systems
  private PhysicsSystem? _physicsSystem;
  private PhysicsSystem2D? _physicsSystem2D;
  private WebApiSystem? _webApi;

  public bool Reload3DRenderSystem = false;
  public bool Reload2DRenderSystem = false;
  public bool ReloadUISystem = false;
  public bool ReloadParticleSystem = false;

  public void UpdateSystems(Entity[] entities, FrameInfo frameInfo) {
    _render3DSystem?.Render(frameInfo);
    _render2DSystem?.Render(frameInfo, entities.DistinctI2D());
    _shadowRenderSystem?.Render(frameInfo);
    _directionaLightSystem?.Render(frameInfo);
    _pointLightSystem?.Render(frameInfo);
    _postProcessingSystem?.Render(frameInfo);
  }

  public void UpdateSystems2(Entity[] entities, FrameInfo frameInfo) {
    _guizmoRenderSystem?.Render(frameInfo);
    _renderDebugSystem?.Render(frameInfo, entities.DistinctInterface<IDebugRenderObject>());
    _particleSystem?.Render(frameInfo);
    // _renderUISystem?.DrawUI(frameInfo, _canvas);
  }

  public Task UpdateCalculationSystems(Entity[] entities) {
    _physicsSystem?.Tick(entities);
    _physicsSystem2D?.Tick(entities);
    _particleSystem?.Update();
    _particleSystem?.Collect();
    return Task.CompletedTask;
  }

  public void ValidateSystems(
    ReadOnlySpan<Entity> entities,
    nint allocator,
    IDevice device,
    IRenderer renderer,
    Dictionary<string, IDescriptorSetLayout> layouts,
    PipelineConfigInfo pipelineConfigInfo,
    ref TextureManager textureManager
  ) {
    if (_render3DSystem != null) {
      var modelEntities = entities.DistinctInterface<IRender3DElement>();
      if (modelEntities.Length < 1) return;
      var sizes = _render3DSystem.CheckSizes(modelEntities);
      var textures = _render3DSystem.CheckTextures(modelEntities);
      if (!sizes || !textures || Reload3DRenderSystem) {
        Reload3DRenderSystem = false;
        Reload3DRenderer(allocator, device, renderer, layouts, ref textureManager, pipelineConfigInfo, entities);
      }
    }

    if (_render2DSystem != null) {
      var spriteEntities = entities.ToArray().DistinctI2D();
      if (spriteEntities.Length < 1) return;
      var sizes = _render2DSystem.CheckSizes(spriteEntities);
      // var textures = _render2DSystem.CheckTextures(spriteEntities);
      if (!sizes || Reload2DRenderSystem) {
        Reload2DRenderSystem = false;
        Reload2DRenderer(allocator, device, renderer, layouts["Global"].GetDescriptorSetLayoutPointer(), ref textureManager, pipelineConfigInfo, entities);
      }
    }

    // if (_renderUISystem != null) {
    //   if (canvasEntities.Length < 1) return;
    //   var sizes = _renderUISystem.CheckSizes(canvasEntities, _canvas);
    //   var textures = _renderUISystem.CheckTextures(canvasEntities);
    //   if (!sizes || !textures || ReloadUISystem) {
    //     ReloadUISystem = false;
    //     ReloadUIRenderer(allocator, device, renderer, layouts["Global"].GetDescriptorSetLayout(), ref textureManager, pipelineConfigInfo);
    //   }
    // }

    if (_particleSystem != null) {
      var particles = _particleSystem.Validate();
      if (!particles || ReloadParticleSystem) {
        ReloadParticleSystem = false;
        ReloadParticleRenderer(allocator, device, renderer, layouts["Global"].GetDescriptorSetLayoutPointer(), ref textureManager, new ParticlePipelineConfigInfo());
      }
    }
  }

  public void Setup(
    Application app,
    SystemCreationFlags creationFlags,
    SystemConfiguration systemConfiguration,
    nint allocator,
    IDevice device,
    IRenderer renderer,
    Dictionary<string, IDescriptorSetLayout> layouts,
    PipelineConfigInfo configInfo,
    ref TextureManager textureManager
  ) {
    SystemCreator.CreateSystems(
      app.Systems,
      creationFlags,
      systemConfiguration,
      allocator,
      (VulkanDevice)device,
      renderer,
      layouts,
      configInfo
    );
    // _subpassConnectorSystem = new(allocator, device, renderer, layouts, new SecondSubpassPipeline());
    _postProcessingSystem = new(allocator, device, renderer, systemConfiguration, layouts, new PostProcessingPipeline());

    var entities = app.GetEntities();
    var objs3D = entities.DistinctInterface<IRender3DElement>();
    _render3DSystem?.Setup(objs3D, ref textureManager);
    _render2DSystem?.Setup(entities.ToArray().DistinctI2D(), ref textureManager);
    // _renderUISystem?.Setup(Canvas, ref textureManager);
    _directionaLightSystem?.Setup();
    _pointLightSystem?.Setup();
    _particleSystem?.Setup(ref textureManager);
    _physicsSystem?.Init(entities.ToArray());
    _physicsSystem2D?.Init(entities.ToArray());
  }

  public void SetupRenderDatas(ReadOnlySpan<Entity> entities, ref TextureManager textureManager, Renderer renderer) {
    _render3DSystem?.Setup(entities.DistinctInterface<IRender3DElement>(), ref textureManager);
    _render2DSystem?.Setup(entities.ToArray().DistinctI2D(), ref textureManager);
    // _renderUISystem?.Setup(canvas, ref textureManager);
  }

  public void Reload3DRenderer(
    nint allocator,
    IDevice device,
    IRenderer renderer,
    Dictionary<string, IDescriptorSetLayout> externalLayouts,
    ref TextureManager textureManager,
    PipelineConfigInfo pipelineConfig,
    ReadOnlySpan<Entity> entities
  ) {
    _render3DSystem?.Dispose();
    _render3DSystem = new Render3DSystem(
      allocator,
      device,
      renderer,
      externalLayouts,
      pipelineConfig
    );
    _render3DSystem?.Setup(entities.DistinctInterface<IRender3DElement>(), ref textureManager);
  }

  public void Reload2DRenderer(
    nint allocator,
    IDevice device,
    IRenderer renderer,
    VkDescriptorSetLayout globalLayout,
    ref TextureManager textureManager,
    PipelineConfigInfo pipelineConfig,
    ReadOnlySpan<Entity> entities
  ) {
    // _render2DSystem?.Dispose();
    // _render2DSystem = new Render2DSystem(
    //   allocator,
    //   device,
    //   renderer,
    //   globalLayout,
    //   pipelineConfig
    // );
    _render2DSystem?.Setup(entities.ToArray().DistinctI2D(), ref textureManager);
  }

  public void ReloadUIRenderer(
    nint allocator,
    IDevice device,
    IRenderer renderer,
    VkDescriptorSetLayout globalLayout,
    ref TextureManager textureManager,
    PipelineConfigInfo pipelineConfig
  ) {
    _renderUISystem?.Dispose();
    _renderUISystem = new RenderUISystem(
      allocator,
      (VulkanDevice)device,
      renderer,
      globalLayout,
      pipelineConfig
    );
    // _renderUISystem?.Setup(_canvas, ref textureManager);
  }

  public void ReloadParticleRenderer(
    nint allocator,
    IDevice device,
    IRenderer renderer,
    VkDescriptorSetLayout globalLayout,
    ref TextureManager textureManager,
    PipelineConfigInfo pipelineConfig
  ) {
    _particleSystem?.Dispose();
    _particleSystem = new ParticleSystem(
      allocator,
      device,
      renderer,
      globalLayout,
      pipelineConfig
    );
    _particleSystem?.Setup(ref textureManager);
  }

  public Render3DSystem Render3DSystem {
    get { return _render3DSystem ?? null!; }
    set { _render3DSystem = value; }
  }

  public Render2DSystem Render2DSystem {
    get { return _render2DSystem ?? null!; }
    set { _render2DSystem = value; }
  }

  public RenderUISystem RenderUISystem {
    get { return _renderUISystem ?? null!; }
    set { _renderUISystem = value; }
  }

  public PhysicsSystem PhysicsSystem {
    get { return _physicsSystem ?? null!; }
    set {
      _physicsSystem = value;
    }
  }

  public PhysicsSystem2D PhysicsSystem2D {
    get { return _physicsSystem2D ?? null!; }
    set {
      _physicsSystem2D = value;
    }
  }

  public RenderDebugSystem RenderDebugSystem {
    get { return _renderDebugSystem ?? null!; }
    set { _renderDebugSystem = value; }
  }

  public DirectionalLightSystem DirectionalLightSystem {
    get { return _directionaLightSystem ?? null!; }
    set { _directionaLightSystem = value; }
  }

  public PointLightSystem PointLightSystem {
    get { return _pointLightSystem ?? null!; }
    set { _pointLightSystem = value; }
  }

  public GuizmoRenderSystem GuizmoRenderSystem {
    get { return _guizmoRenderSystem ?? null!; }
    set { _guizmoRenderSystem = value; }
  }

  public ParticleSystem ParticleSystem {
    get { return _particleSystem ?? null!; }
    set { _particleSystem = value; }
  }

  public ShadowRenderSystem ShadowRenderSystem {
    get { return _shadowRenderSystem ?? null!; }
    set { _shadowRenderSystem = value; }
  }

  public WebApiSystem WebApi {
    get { return _webApi ?? null!; }
    set { _webApi = value; }
  }

  public PostProcessingSystem? PostProcessingSystem => _postProcessingSystem;

  public void Dispose() {
    _postProcessingSystem?.Dispose();
    _render3DSystem?.Dispose();
    _render2DSystem?.Dispose();
    _renderUISystem?.Dispose();
    _physicsSystem?.Dispose();
    _physicsSystem2D?.Dispose();
    _renderDebugSystem?.Dispose();
    _guizmoRenderSystem?.Dispose();
    _directionaLightSystem?.Dispose();
    _pointLightSystem?.Dispose();
    _webApi?.Dispose();
    _particleSystem?.Dispose();
    _shadowRenderSystem?.Dispose();
    GC.SuppressFinalize(this);
  }
}
