using System.Diagnostics;
using Neko.EntityComponentSystem;
using Neko.Physics;
using Neko.Physics.Interfaces;
using Neko.Rendering.Renderer3D;
using Neko.Vulkan;

namespace Neko.Globals;

public static class PerfMonitor {
  public static uint TextureBindingsIn3DRenderer { get; set; }
  public static uint VertexBindingsIn3DRenderer { get; set; }
  public static uint NumberOfObjectsRenderedIn3DRenderer { get; set; }
  public static double Render3DComputeTime { get; set; }

  public static Stopwatch ComunnalStopwatch { get; } = Stopwatch.StartNew();

  private static bool s_debug = true;

  public static void Clear3DRendererInfo() {
    TextureBindingsIn3DRenderer = 0;
    VertexBindingsIn3DRenderer = 0;
    NumberOfObjectsRenderedIn3DRenderer = 0;
  }

  public static void ChangeWireframeMode() {
    Application.Instance.CurrentPipelineConfig = Application.Instance.CurrentPipelineConfig?.GetType() == typeof(VkPipelineConfigInfo)
      ? new VertexDebugPipeline()
      : new VkPipelineConfigInfo();
    Application.Instance.Systems.Reload3DRenderSystem = true;
    Application.Instance.Systems.Reload2DRenderSystem = true;
  }

  public static void ChangeDebugVisiblity() {
    s_debug = !s_debug;
    var entities = Application.Instance.GetEntities();
    var debugObjects = Application.Instance.DebugMeshes.Values.ToArray();
    foreach (var entity in debugObjects) {
      if (s_debug) {
        entity?.Enable();
      } else {
        entity?.Disable();
      }
    }
  }

  public static bool IsDebug => s_debug;
}