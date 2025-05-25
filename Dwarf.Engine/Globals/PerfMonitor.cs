using System.Diagnostics;
using Dwarf.EntityComponentSystem;
using Dwarf.Physics;
using Dwarf.Physics.Interfaces;
using Dwarf.Rendering.Renderer3D;
using Dwarf.Vulkan;

namespace Dwarf.Globals;

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
    Application.Instance.CurrentPipelineConfig = Application.Instance.CurrentPipelineConfig.GetType() == typeof(VkPipelineConfigInfo)
      ? new VertexDebugPipeline()
      : new VkPipelineConfigInfo();
    Application.Instance.Systems.Reload3DRenderSystem = true;
    Application.Instance.Systems.Reload2DRenderSystem = true;
  }

  public static void ChangeDebugVisiblity() {
    s_debug = !s_debug;
    var entities = Application.Instance.GetEntities();
    var debugObjects = entities.DistinctInterface<IDebugRenderObject>();
    foreach (var entity in debugObjects) {
      var e = entity.GetDrawable<IDebugRenderObject>() as IDebugRenderObject;
      if (s_debug) {
        e?.Enable();
      } else {
        e?.Disable();
      }
    }
  }

  public static bool IsDebug => s_debug;
}