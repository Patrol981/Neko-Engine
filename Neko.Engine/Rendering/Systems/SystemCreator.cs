using Dwarf.AbstractionLayer;
using Dwarf.Extensions.Logging;
using Dwarf.Rendering.Particles;
using Dwarf.Rendering.PostProcessing;
using Dwarf.Rendering.Renderer3D;
using Dwarf.Vulkan;

namespace Neko.Rendering;

[Flags]
public enum SystemCreationFlags {
  None = 0,
  Renderer3D = 1,
  Renderer2D = 1 << 1,
  RendererUI = 1 << 2,
  Physics3D = 1 << 3,
  DirectionalLight = 1 << 4,
  PointLights = 1 << 5,
  Guizmos = 1 << 6,
  WebApi = 1 << 7,
  Particles = 1 << 8,
  Shadows = 1 << 9,
  Physics2D = 1 << 10,
  DebugRenderer = 1 << 11,
  Networking = 1 << 12,
  Animations = 1 << 13
}

public record SystemConfiguration {
  public Neko.Physics.Backends.BackendKind PhysiscsBackend { get; init; }
  public PostProcessingConfigurationFlag PostProcessingFlag { get; init; }
  public string[]? PostProcessInputTextures { get; init; }
  public ApplicationType ApplicationType { get; init; } = ApplicationType.Default;
  public string[]? WebAllowOrigins { get; init; }

  public static SystemConfiguration Default => new() {
    PhysiscsBackend = Physics.Backends.BackendKind.Default,
    PostProcessingFlag = PostProcessingConfigurationFlag.Custom,
    ApplicationType = ApplicationType.Default,
    WebAllowOrigins = []
  };

  public static SystemConfiguration GetDefault() => new() {
    PhysiscsBackend = Physics.Backends.BackendKind.Default,
    PostProcessingFlag = PostProcessingConfigurationFlag.Custom,
    ApplicationType = ApplicationType.Default,
    WebAllowOrigins = []
  };
}

public class SystemCreator {
  public static void CreateSystems(
    Application app,
    SystemCollection systemCollection,
    SystemCreationFlags flags,
    SystemConfiguration systemConfig,
    nint allocator,
    IDevice device,
    IRenderer renderer,
    Dictionary<string, IDescriptorSetLayout> layouts,
    IPipelineConfigInfo configInfo = null!
  ) {
    var hasRenderer3D = flags.HasFlag(SystemCreationFlags.Renderer3D);
    var hasRenderer2D = flags.HasFlag(SystemCreationFlags.Renderer2D);
    var hasRendererUI = flags.HasFlag(SystemCreationFlags.RendererUI);
    var usePhysics3D = flags.HasFlag(SystemCreationFlags.Physics3D);
    var usePhysics2D = flags.HasFlag(SystemCreationFlags.Physics2D);
    var hasDirectionalLight = flags.HasFlag(SystemCreationFlags.DirectionalLight);
    var hasPointLights = flags.HasFlag(SystemCreationFlags.PointLights);
    var hasGuizmos = flags.HasFlag(SystemCreationFlags.Guizmos);
    var hasWebApi = flags.HasFlag(SystemCreationFlags.WebApi);
    var hasParticles = flags.HasFlag(SystemCreationFlags.Particles);
    var hasShadows = flags.HasFlag(SystemCreationFlags.Shadows);
    var hasDebugRenderer = flags.HasFlag(SystemCreationFlags.DebugRenderer);
    var hasNetworking = flags.HasFlag(SystemCreationFlags.Networking);
    var hasAnimations = flags.HasFlag(SystemCreationFlags.Animations);

    if (hasRendererUI) {
      Logger.Info("[SYSTEM CREATOR] Creating UI Renderer");
      systemCollection.RenderUISystem =
        new(allocator, device, renderer, layouts["Global"], configInfo);
    }

    if (hasRenderer3D) {
      Logger.Info("[SYSTEM CREATOR] Creating 3D Renderer");
      systemCollection.Render3DSystem =
        new(allocator, device, renderer, layouts, new ModelPipelineConfig());
    }

    if (hasDebugRenderer) {
      Logger.Info("[SYSTEM CREATOR] Creating Debug Renderer");
      var debugConfig = new VertexDebugPipeline();
      systemCollection.RenderDebugSystem =
        new(allocator, device, renderer, layouts["Global"], debugConfig);
    }

    if (hasRenderer2D) {
      Logger.Info("[SYSTEM CREATOR] Creating 2D Renderer");
      systemCollection.Render2DSystem =
        new(allocator, device, renderer, layouts["Global"], configInfo);
    }

    if (usePhysics3D) {
      Logger.Info("[SYSTEM CREATOR] Setting up Physics 3D");
      systemCollection.PhysicsSystem = new(systemConfig.PhysiscsBackend);
    }

    if (usePhysics2D) {
      Logger.Info("[SYSTEM CREATOR] Setting up Physics 2D");
      systemCollection.PhysicsSystem2D = new(systemConfig.PhysiscsBackend);
    }

    if (hasDirectionalLight) {
      Logger.Info("[SYSTEM CREATOR] Creating Directional Light System");
      systemCollection.DirectionalLightSystem =
        new(allocator, device, renderer, layouts["Global"]);
    }

    if (hasPointLights) {
      Logger.Info("[SYSTEM CREATOR] Creating Point Light System");
      systemCollection.PointLightSystem =
        new(allocator, device, renderer, layouts["Global"]);
    }

    if (hasGuizmos) {
      Logger.Info("[SYSTEM CREATOR] Creating Guizmos Rendering System");
      systemCollection.GuizmoRenderSystem =
        new(allocator, device, renderer, layouts["Global"]);
    }

    if (hasWebApi) {
      Logger.Info("[SYSTEM CREATOR] Creating WebApi");
      systemCollection.WebApi = new(app: Application.Instance);
    }

    if (hasNetworking) {
      Logger.Info("[SYSTEM CREATOr] Creating Networking");

      if (Application.ApplicationMode == ApplicationType.Headless) {
        // systemCollection.NetSystem = new(Application.Instance);
      } else {
        // systemCollection.NetClientSystem = new();
      }

    }

    if (hasParticles) {
      Logger.Info("[SYSTEM CREATOR] Creating Particle System");
      systemCollection.ParticleSystem =
        new(allocator, device, renderer, layouts["Global"], new ParticlePipelineConfigInfo());
    }

    if (hasShadows) {
      Logger.Info("[SYSTEM CREATOR] Creating Shadows System");
      systemCollection.ShadowRenderSystem =
        new(allocator, device, renderer, systemConfig, layouts, new ModelPipelineConfig());
    }
  }
}
