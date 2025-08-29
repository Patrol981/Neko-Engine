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
using Dwarf.Rendering.Renderer2D.Interfaces;
using Dwarf.Rendering.Renderer3D;
using Dwarf.Rendering.Shadows;
using Dwarf.Vulkan;

using Vortice.Vulkan;
using Entity = Dwarf.EntityComponentSystem.Entity;

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
  private SignalRSystem? _netSystem;
  private SignalRClientSystem? _netClientSystem;

  public bool Reload3DRenderSystem = false;
  public bool Reload2DRenderSystem = false;
  public bool ReloadUISystem = false;
  public bool ReloadParticleSystem = false;

  public void UpdateSystems(Application app, FrameInfo frameInfo) {
    _render3DSystem?.Render(frameInfo);
    _render2DSystem?.Render(frameInfo, app.Sprites.Values.ToArray());
    _shadowRenderSystem?.Render(frameInfo);
    _directionaLightSystem?.Render(frameInfo);
    _pointLightSystem?.Render(frameInfo);

    // _renderDebugSystem?.Render(frameInfo, entities.DistinctInterface<IDebugRenderObject>());
    _renderDebugSystem?.Render(frameInfo, app.DebugMeshes.Values.ToArray());
    _particleSystem?.Render(frameInfo);
  }

  public void UpdateSystems2(Application app, FrameInfo frameInfo) {
    // _guizmoRenderSystem?.Render(frameInfo);
    // _renderDebugSystem?.Render(frameInfo, entities.DistinctInterface<IDebugRenderObject>());
    // _particleSystem?.Render(frameInfo);
    // _renderUISystem?.DrawUI(frameInfo, _canvas);

    _postProcessingSystem?.Render(frameInfo);
  }

  public Task UpdateCalculationSystems(Application app) {
    _physicsSystem?.Tick(app.Rigidbodies.Values.ToArray());
    _physicsSystem2D?.Tick(app.Rigidbodies2D.Values.ToArray());
    _particleSystem?.Update();
    _particleSystem?.Collect();
    return Task.CompletedTask;
  }

  public void ValidateSystems(
    Application app,
    nint allocator,
    IDevice device,
    IRenderer renderer,
    Dictionary<string, IDescriptorSetLayout> layouts,
    IPipelineConfigInfo pipelineConfigInfo,
    ref TextureManager textureManager
  ) {
    if (_render3DSystem != null) {
      var modelEntities = app.Drawables3D.Values.ToArray();
      if (modelEntities.Length < 1) return;
      var sizes = _render3DSystem.CheckSizes(modelEntities);
      var textures = _render3DSystem.CheckTextures(modelEntities);
      if (!sizes || !textures || Reload3DRenderSystem) {
        Reload3DRenderSystem = false;
        Reload3DRenderer(
          allocator,
          device,
          renderer,
          layouts,
          ref textureManager,
          pipelineConfigInfo,
          modelEntities
        );
      }
    }

    if (_render2DSystem != null) {
      var spriteEntities = app.Sprites.Values.ToArray();
      if (spriteEntities.Length < 1) return;
      var sizes = _render2DSystem.CheckSizes(spriteEntities);
      // var textures = _render2DSystem.CheckTextures(spriteEntities);
      if (!sizes || Reload2DRenderSystem) {
        Reload2DRenderSystem = false;
        Reload2DRenderer(
          allocator,
          device,
          renderer,
          layouts["Global"],
          ref textureManager,
          pipelineConfigInfo,
          spriteEntities
        );
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
        ReloadParticleRenderer(allocator, device, renderer, layouts["Global"], ref textureManager, new ParticlePipelineConfigInfo());
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
    IPipelineConfigInfo configInfo,
    ref TextureManager textureManager
  ) {
    SystemCreator.CreateSystems(
      app.Systems,
      creationFlags,
      systemConfiguration,
      allocator,
      (VulkanDevice)device,
      renderer,
      textureManager,
      layouts,
      configInfo
    );
    // _subpassConnectorSystem = new(allocator, device, renderer, layouts, new SecondSubpassPipeline());
    // _postProcessingSystem = new(allocator, device, renderer, textureManager, systemConfiguration, layouts, new PostProcessingPipeline());

    _render3DSystem?.Setup(app.Drawables3D.Values.ToArray(), ref textureManager);

    var drawables = app.Entities.FlattenDrawable2D();
    _render2DSystem?.Setup(drawables, ref textureManager);
    // _renderUISystem?.Setup(Canvas, ref textureManager);
    _directionaLightSystem?.Setup();
    _pointLightSystem?.Setup();
    _particleSystem?.Setup(ref textureManager);
    _physicsSystem?.Init(app.Entities.ToArray());
    _physicsSystem2D?.Init(app.Entities.ToArray());
  }

  public void SetupHeadless(
    Application app,
    SystemCreationFlags creationFlags,
    SystemConfiguration systemConfiguration
  ) {
    SystemCreator.CreateSystems(
      app.Systems,
      creationFlags,
      systemConfiguration,
      IntPtr.Zero,
      null!,
      null!,
      null!,
      null!,
      null!
    );
  }

  public void Reload3DRenderer(
    nint allocator,
    IDevice device,
    IRenderer renderer,
    Dictionary<string, IDescriptorSetLayout> externalLayouts,
    ref TextureManager textureManager,
    IPipelineConfigInfo pipelineConfig,
    ReadOnlySpan<IRender3DElement> renderables
  ) {
    _render3DSystem?.Dispose();
    _render3DSystem = new Render3DSystem(
      allocator,
      device,
      renderer,
      textureManager,
      externalLayouts,
      pipelineConfig
    );
    _render3DSystem?.Setup(renderables, ref textureManager);
  }

  public void Reload2DRenderer(
    nint allocator,
    IDevice device,
    IRenderer renderer,
    IDescriptorSetLayout globalLayout,
    ref TextureManager textureManager,
    IPipelineConfigInfo pipelineConfig,
    ReadOnlySpan<IDrawable2D> drawables
  ) {
    // _render2DSystem?.Dispose();
    // _render2DSystem = new Render2DSystem(
    //   allocator,
    //   device,
    //   renderer,
    //   globalLayout,
    //   pipelineConfig
    // );
    _render2DSystem?.Setup(drawables, ref textureManager);
  }

  public void ReloadUIRenderer(
    nint allocator,
    IDevice device,
    IRenderer renderer,
    IDescriptorSetLayout globalLayout,
    ref TextureManager textureManager,
    IPipelineConfigInfo pipelineConfig
  ) {
    _renderUISystem?.Dispose();
    _renderUISystem = new RenderUISystem(
      allocator,
      (VulkanDevice)device,
      renderer,
      textureManager,
      globalLayout,
      pipelineConfig
    );
    // _renderUISystem?.Setup(_canvas, ref textureManager);
  }

  public void ReloadParticleRenderer(
    nint allocator,
    IDevice device,
    IRenderer renderer,
    IDescriptorSetLayout globalLayout,
    ref TextureManager textureManager,
    IPipelineConfigInfo pipelineConfig
  ) {
    _particleSystem?.Dispose();
    _particleSystem = new ParticleSystem(
      allocator,
      device,
      renderer,
      textureManager,
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

  public PostProcessingSystem PostProcessingSystem {
    get => _postProcessingSystem ?? null!;
    set => _postProcessingSystem = value;
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

  public SignalRSystem NetSystem {
    get => _netSystem ?? null!;
    set => _netSystem = value;
  }

  public SignalRClientSystem NetClientSystem {
    get => _netClientSystem ?? null!;
    set => _netClientSystem = value;
  }

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
    _netSystem?.Dispose();
    _netClientSystem?.Dispose();
    _particleSystem?.Dispose();
    _shadowRenderSystem?.Dispose();
    GC.SuppressFinalize(this);
  }
}
