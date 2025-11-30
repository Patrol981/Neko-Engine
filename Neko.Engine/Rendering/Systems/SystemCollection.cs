using System.Runtime.CompilerServices;
using Neko.AbstractionLayer;
using Neko.Animations;
using Neko.EntityComponentSystem;
using Neko.Extensions.Logging;
using Neko.Networking;
using Neko.Physics;
using Neko.Rendering.DebugRenderer;
using Neko.Rendering.Guizmos;
using Neko.Rendering.Lightning;
using Neko.Rendering.Particles;
using Neko.Rendering.PostProcessing;
using Neko.Rendering.Renderer2D;
using Neko.Rendering.Renderer2D.Interfaces;
using Neko.Rendering.Renderer2D.Models;
using Neko.Rendering.Renderer3D;
using Neko.Rendering.Shadows;
using Neko.Vulkan;

using ZLinq;

namespace Neko.Rendering;

public class SystemCollection : IDisposable {
  // Render Systems
  public Render3DSystem? Render3DSystem { get; set; }
  public SkinnedRenderSystem? SkinnedRenderSystem { get; set; }
  public StaticRenderSystem? StaticRenderSystem { get; set; }
  public CustomShaderRender3DSystem? CustomShaderRender3DSystem { get; set; }
  public Render2DSystem? Render2DSystem { get; set; }
  public CustomShaderRender2DSystem? CustomShaderRender2DSystem { get; set; }
  public RenderUISystem? RenderUISystem { get; set; }
  public RenderDebugSystem? RenderDebugSystem { get; set; }
  public DirectionalLightSystem? DirectionalLightSystem { get; set; }
  public PointLightSystem? PointLightSystem { get; set; }
  public GuizmoRenderSystem? GuizmoRenderSystem { get; set; }
  public ParticleSystem? ParticleSystem { get; set; }
  public ShadowRenderSystem? ShadowRenderSystem { get; set; }
  public AnimationSystem? AnimationSystem { get; set; }

  public PostProcessingSystem? PostProcessingSystem { get; set; }

  // Calculation Systems
  public PhysicsSystem? PhysicsSystem { get; set; }
  public PhysicsSystem2D? PhysicsSystem2D { get; set; }
  public WebApiSystem? WebApi { get; set; }
  // public SignalRSystem? NetSystem { get; set; }
  // public SignalRClientSystem? NetClientSystem { get; set; }

  public bool Reload3DRenderSystem = false;
  public bool Reload2DRenderSystem = false;
  public bool ReloadUISystem = false;
  public bool ReloadParticleSystem = false;

  public void OnEntityChanged() {
    var app = Application.Instance;
    var textureManager = app.TextureManager;

    Application.Mutex.WaitOne();

    if (StaticRenderSystem != null) {
      var staticNodes = app.Drawables3D.Values.AsValueEnumerable()
        .Where(x => x.CustomShader.Name == CommonConstants.SHADER_INFO_NAME_UNSET)
        .Where(x => !x.IsSkinned)
        .ToArray();
      StaticRenderSystem.Invalidate(staticNodes);
    }

    if (SkinnedRenderSystem != null) {
      var skinnedNodes = app.Drawables3D.Values.AsValueEnumerable()
        .Where(x => x.CustomShader.Name == CommonConstants.SHADER_INFO_NAME_UNSET)
        .Where(x => x.IsSkinned)
        .ToArray();
      SkinnedRenderSystem.Invalidate(skinnedNodes);
    }

    if (CustomShaderRender3DSystem != null) {
      var customNodes = app.Drawables3D.Values.AsValueEnumerable()
        .Where(x => x.CustomShader.Name != CommonConstants.SHADER_INFO_NAME_UNSET)
        .Where(x => !x.IsSkinned)
        .ToArray();
      CustomShaderRender3DSystem.Invalidate(customNodes);
    }

    if (Render2DSystem != null) {
      var i2D = app.Sprites.FlattenDrawable2D().AsValueEnumerable();
      var sprites = i2D
        .Where(x => x.CustomShader.Name == CommonConstants.SHADER_INFO_NAME_UNSET)
        .ToArray();
      Render2DSystem.Invalidate(sprites);
    }

    if (CustomShaderRender2DSystem != null) {
      var i2D = app.Sprites.FlattenDrawable2D().AsValueEnumerable();
      var customSprites = i2D
        .Where(x => x.CustomShader.Name != CommonConstants.SHADER_INFO_NAME_UNSET)
        .ToArray();
      CustomShaderRender2DSystem.Invalidate(customSprites);
    }

    // ValidateSystems(
    //   app,
    //   app.Allocator, app.Device, app.Renderer,
    //   app.DescriptorSetLayouts,
    //   app.CurrentPipelineConfig,
    //   ref textureManager
    // );

    Application.Mutex.ReleaseMutex();

    Logger.Warn("ENTITY CHANGED EVENT");
  }

  public unsafe void UpdateSystems(Application app, FrameInfo frameInfo, GlobalUniformBufferObject* gbo) {
    if (PointLightSystem != null) {
      PointLightSystem.Update(app.Lights.Values.AsValueEnumerable().ToArray(), out var pointLights);
      if (pointLights.Length > 1) {
        gbo->PointLightsLength = pointLights.Length;
        fixed (PointLight* pPointLights = pointLights) {
          app.StorageCollection.WriteBuffer(
            "PointStorage",
            frameInfo.FrameIndex,
            (nint)pPointLights,
            (ulong)Unsafe.SizeOf<PointLight>() * CommonConstants.MAX_POINT_LIGHTS_COUNT
          );
        }
      } else { gbo->PointLightsLength = 0; }
    }

    StaticRenderSystem?.Update(frameInfo, app.Meshes, out _);
    SkinnedRenderSystem?.Update(frameInfo, app.Meshes);
    CustomShaderRender3DSystem?.Update(frameInfo, app.Meshes, app.Entities);

    Render2DSystem?.Update(frameInfo);
    CustomShaderRender2DSystem?.Update(frameInfo, app.Entities);
  }

  public void CheckStorageSizes(
    Application app,
    FrameInfo frameInfo,
    Dictionary<string, IDescriptorSetLayout> _descriptorSetLayouts
  ) {
    if (StaticRenderSystem != null && SkinnedRenderSystem != null) {
      app.StorageCollection.CheckSize(
        "StaticObjectStorage",
        frameInfo.FrameIndex,
        StaticRenderSystem.LastKnownElemCount,
        _descriptorSetLayouts["ObjectData"],
        default
      );

      app.StorageCollection.CheckSize(
        "SkinnedObjectStorage",
        frameInfo.FrameIndex,
        (int)SkinnedRenderSystem.LastKnownSkinnedElemCount,
        _descriptorSetLayouts["ObjectData"],
        default
      );

      app.StorageCollection.CheckSize(
        "JointsStorage",
        frameInfo.FrameIndex,
        StaticRenderSystem.LastKnownElemCount,
        _descriptorSetLayouts["JointsBuffer"],
        default
      );

      app.StorageCollection.CheckSize(
        "CustomShaderObjectStorage",
        frameInfo.FrameIndex,
        CustomShaderRender3DSystem?.LastKnownElemCount ?? 0,
        _descriptorSetLayouts["CustomShaderObjectData"],
        default
      );
    }

    if (Render2DSystem != null) {
      app.StorageCollection.CheckSize(
        "SpriteStorage",
        frameInfo.FrameIndex,
        Render2DSystem.LastKnownElemCount,
        _descriptorSetLayouts["SpriteData"],
        default
      );

      app.StorageCollection.CheckSize(
        "CustomSpriteStorage",
        frameInfo.FrameIndex,
        CustomShaderRender2DSystem?.LastKnownElemCount ?? 0,
        _descriptorSetLayouts["CustomSpriteData"],
        default
      );
    }
  }

  public void RenderSystems(Application app, FrameInfo frameInfo) {

    // if (Render3DSystem != null) {
    //   Render3DSystem.Render(
    //     app.Drawables3D.Values.AsValueEnumerable()
    //       .Where(x => x.CustomShader.Name == CommonConstants.SHADER_INFO_NAME_UNSET)
    //       .ToArray(),
    //     app.Meshes,
    //     frameInfo,
    //     out var animatedNodes
    //   );
    //   AnimationSystem?.Update(animatedNodes);
    //   // _animationSystem?.Update(_render3DSystem.SkinnedNodesCache);
    // }
    // ReadOnlySpan<Node> staticNodes = [];
    StaticRenderSystem?.Render(
      app.Meshes,
      frameInfo,
      out _
    );
    if (SkinnedRenderSystem != null) {
      SkinnedRenderSystem.Render(
        app.Meshes,
        frameInfo,
        out var animatedNodes
      );
      AnimationSystem?.Update([.. animatedNodes]);
    }
    CustomShaderRender3DSystem?.Render(frameInfo, app.Meshes);

    Render2DSystem?.Render(frameInfo);
    CustomShaderRender2DSystem?.Render(frameInfo);

    ShadowRenderSystem?.Render(frameInfo);
    DirectionalLightSystem?.Render(frameInfo);
    PointLightSystem?.Render(frameInfo);

    GuizmoRenderSystem?.Render(frameInfo);

    // _renderDebugSystem?.Render(frameInfo, entities.DistinctInterface<IDebugRenderObject>());
    RenderDebugSystem?.Render(frameInfo, app.DebugMeshes.Values.ToArray());
    ParticleSystem?.Render(frameInfo);
  }

  public void RenderSystems2(Application app, FrameInfo frameInfo) {
    // _renderDebugSystem?.Render(frameInfo, entities.DistinctInterface<IDebugRenderObject>());
    // _particleSystem?.Render(frameInfo);
    // _renderUISystem?.DrawUI(frameInfo, _canvas);

    PostProcessingSystem?.Render(frameInfo);
  }

  public Task UpdateCalculationSystems(Application app) {
    PhysicsSystem?.Tick(app.Rigidbodies.Values.ToArray());
    PhysicsSystem2D?.Tick(app.Rigidbodies2D.Values.ToArray());
    ParticleSystem?.Update();
    ParticleSystem?.Collect();
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
    var modelEntities = app.Drawables3D.Values.AsValueEnumerable();
    if (SkinnedRenderSystem != null) {
      var skinnedEntities = modelEntities
        .Where(x => x.IsSkinned)
        .ToArray();
      if (skinnedEntities.Length > 1) {
        var sizes = SkinnedRenderSystem.CheckSizes(skinnedEntities);
        if (!sizes || Reload3DRenderSystem) {
          ReloadSkinnedRenderer(
           app,
           allocator,
           device,
           renderer,
           layouts,
           ref textureManager,
           pipelineConfigInfo,
           skinnedEntities
         );
        }
      }
    }

    if (StaticRenderSystem != null) {
      var staticEntities = modelEntities
        .Where(x => !x.IsSkinned)
        .ToArray();

      if (staticEntities.Length > 1) {
        var sizes = StaticRenderSystem.CheckSizes(staticEntities);
        if (!sizes || Reload3DRenderSystem) {
          ReloadStaticRenderer(
           app,
           allocator,
           device,
           renderer,
           layouts,
           ref textureManager,
           pipelineConfigInfo,
           staticEntities
          );
        }
      }
    }

    // if (Render3DSystem != null) {
    //   var modelEntities = app.Drawables3D.Values.ToArray();
    //   if (modelEntities.Length < 1) return;
    //   var sizes = Render3DSystem.CheckSizes(modelEntities);
    //   var textures = Render3DSystem.CheckTextures(modelEntities);
    //   if (!sizes || !textures || Reload3DRenderSystem) {
    //     Reload3DRenderSystem = false;
    //     Reload3DRenderer(
    //       app,
    //       allocator,
    //       device,
    //       renderer,
    //       layouts,
    //       ref textureManager,
    //       pipelineConfigInfo,
    //       modelEntities
    //     );
    //   }
    // }

    if (Reload3DRenderSystem) {
      Reload3DRenderSystem = false;
    }

    if (Render2DSystem != null) {
      // var spriteEntities = app.Entities.FlattenDrawable2D();
      var spriteEntities = app.Sprites.FlattenDrawable2D();
      if (spriteEntities.Length < 1) return;
      var sizes = Render2DSystem.CheckSizes(
        spriteEntities.AsValueEnumerable()
          .Where(x => x.CustomShader.Name == CommonConstants.SHADER_INFO_NAME_UNSET)
          .ToArray()
      );
      // var textures = _render2DSystem.CheckTextures(spriteEntities);
      if (!sizes || Reload2DRenderSystem) {
        Reload2DRenderSystem = false;
        Reload2DRenderer(
          app,
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

    if (ParticleSystem != null) {
      var particles = ParticleSystem.Validate();
      if (!particles || ReloadParticleSystem) {
        ReloadParticleSystem = false;
        ReloadParticleRenderer(app, allocator, device, renderer, layouts["Global"], ref textureManager, new ParticlePipelineConfigInfo());
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
      app,
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

    // Render3DSystem?.Setup(
    //   app.Drawables3D.Values.AsValueEnumerable()
    //     .Where(x => x.CustomShader.Name == CommonConstants.SHADER_INFO_NAME_UNSET)
    //     .ToArray(),
    //   ref textureManager
    // );

    StaticRenderSystem?.Setup(
      app.Drawables3D.Values.AsValueEnumerable()
        .Where(x => x.CustomShader.Name == CommonConstants.SHADER_INFO_NAME_UNSET)
        .Where(x => !x.IsSkinned)
        .ToArray()
    );

    SkinnedRenderSystem?.Setup(
      app.Drawables3D.Values.AsValueEnumerable()
        .Where(x => x.CustomShader.Name == CommonConstants.SHADER_INFO_NAME_UNSET)
        .Where(x => x.IsSkinned)
        .ToArray()
    );

    CustomShaderRender3DSystem?.Setup(
      app.Drawables3D.Values.AsValueEnumerable()
        .Where(x => x.CustomShader.Name != CommonConstants.SHADER_INFO_NAME_UNSET)
        .ToArray()
      );

    // var drawables_old = app.Entities.FlattenDrawable2D();
    var drawables = app.Sprites.FlattenDrawable2D().ToArray();
    Render2DSystem?.Setup(
      drawables.AsValueEnumerable()
        .Where(x => x.CustomShader.Name == CommonConstants.SHADER_INFO_NAME_UNSET)
        .ToArray(),
      ref textureManager
    );
    CustomShaderRender2DSystem?.Setup(
      drawables.AsValueEnumerable()
        .Where(x => x.CustomShader.Name != CommonConstants.SHADER_INFO_NAME_UNSET)
        .ToArray()
    );
    // _renderUISystem?.Setup(Canvas, ref textureManager);
    DirectionalLightSystem?.Setup();
    PointLightSystem?.Setup();
    ParticleSystem?.Setup(ref textureManager);
    PhysicsSystem?.Init(app.Entities.ToArray());
    PhysicsSystem2D?.Init(app.Entities.ToArray());

    app.EntityChangedEvent += OnEntityChanged;
    app.EntityChangedEvent?.Invoke();
  }

  public void SetupHeadless(
    Application app,
    SystemCreationFlags creationFlags,
    SystemConfiguration systemConfiguration
  ) {
    SystemCreator.CreateSystems(
      app,
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
    Application app,
    nint allocator,
    IDevice device,
    IRenderer renderer,
    Dictionary<string, IDescriptorSetLayout> externalLayouts,
    ref TextureManager textureManager,
    IPipelineConfigInfo pipelineConfig,
    ReadOnlySpan<IRender3DElement> renderables
  ) {
    Render3DSystem?.Dispose();
    Render3DSystem = new Render3DSystem(
      app,
      allocator,
      (VulkanDevice)device,
      renderer,
      textureManager,
      externalLayouts,
      pipelineConfig
    );
    Render3DSystem?.Setup(renderables, ref textureManager);
  }

  public void ReloadStaticRenderer(
    Application app,
    nint allocator,
    IDevice device,
    IRenderer renderer,
    Dictionary<string, IDescriptorSetLayout> externalLayouts,
    ref TextureManager textureManager,
    IPipelineConfigInfo pipelineConfig,
    ReadOnlySpan<IRender3DElement> renderables
  ) {
    StaticRenderSystem?.Dispose();
    StaticRenderSystem = new StaticRenderSystem(
      app,
      allocator,
      (VulkanDevice)device,
      renderer,
      textureManager,
      externalLayouts,
      pipelineConfig
    );
    StaticRenderSystem?.Setup(renderables);
    StaticRenderSystem?.Invalidate([.. renderables]);
  }

  public void ReloadSkinnedRenderer(
    Application app,
    nint allocator,
    IDevice device,
    IRenderer renderer,
    Dictionary<string, IDescriptorSetLayout> externalLayouts,
    ref TextureManager textureManager,
    IPipelineConfigInfo pipelineConfig,
    ReadOnlySpan<IRender3DElement> renderables
  ) {
    SkinnedRenderSystem?.Dispose();
    SkinnedRenderSystem = new SkinnedRenderSystem(
      app,
      allocator,
      (VulkanDevice)device,
      renderer,
      textureManager,
      externalLayouts,
      pipelineConfig
    );
    SkinnedRenderSystem?.Setup(renderables);
    SkinnedRenderSystem?.Invalidate([.. renderables]);
  }

  public void Reload2DRenderer(
    Application app,
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
    Render2DSystem?.Setup(
      drawables.AsValueEnumerable()
        .Where(x => x.CustomShader.Name == CommonConstants.SHADER_INFO_NAME_UNSET)
        .ToArray(),
      ref textureManager
    );
    CustomShaderRender2DSystem?.Setup(
      drawables.AsValueEnumerable()
        .Where(x => x.CustomShader.Name != CommonConstants.SHADER_INFO_NAME_UNSET)
        .ToArray()
    );
  }

  public void ReloadUIRenderer(
    Application app,
    nint allocator,
    IDevice device,
    IRenderer renderer,
    IDescriptorSetLayout globalLayout,
    ref TextureManager textureManager,
    IPipelineConfigInfo pipelineConfig
  ) {
    RenderUISystem?.Dispose();
    RenderUISystem = new RenderUISystem(
      app,
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
    Application app,
    nint allocator,
    IDevice device,
    IRenderer renderer,
    IDescriptorSetLayout globalLayout,
    ref TextureManager textureManager,
    IPipelineConfigInfo pipelineConfig
  ) {
    ParticleSystem?.Dispose();
    ParticleSystem = new ParticleSystem(
      app,
      allocator,
      (VulkanDevice)device,
      renderer,
      textureManager,
      globalLayout,
      pipelineConfig
    );
    ParticleSystem?.Setup(ref textureManager);
  }

  public void Dispose() {
    PostProcessingSystem?.Dispose();
    Render3DSystem?.Dispose();
    StaticRenderSystem?.Dispose();
    SkinnedRenderSystem?.Dispose();
    CustomShaderRender3DSystem?.Dispose();
    Render2DSystem?.Dispose();
    CustomShaderRender2DSystem?.Dispose();
    RenderUISystem?.Dispose();
    PhysicsSystem?.Dispose();
    PhysicsSystem2D?.Dispose();
    RenderDebugSystem?.Dispose();
    GuizmoRenderSystem?.Dispose();
    DirectionalLightSystem?.Dispose();
    PointLightSystem?.Dispose();
    WebApi?.Dispose();
    // NetSystem?.Dispose();
    // NetClientSystem?.Dispose();
    ParticleSystem?.Dispose();
    ShadowRenderSystem?.Dispose();

#pragma warning disable CS8601 // Possible null reference assignment.
    Application.Instance.EntityChangedEvent -= OnEntityChanged;
#pragma warning restore CS8601 // Possible null reference assignment.

    GC.SuppressFinalize(this);
  }
}
