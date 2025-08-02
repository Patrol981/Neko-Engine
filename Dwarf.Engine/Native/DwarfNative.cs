using System.Runtime.InteropServices;
using Dwarf;
using Dwarf.EntityComponentSystem;
using Dwarf.Extensions.Logging;
using Dwarf.Globals;
using Dwarf.Rendering;
using Dwarf.Rendering.UI.Utils;
using ImGuiNET;

namespace Dwarf.Native;

#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type

/// <summary>
/// This is an AOT wrapper for DwarfEngine, you don't need to use it if you are not using AOT linking
/// </summary>
public static partial class DwarfNativeInterop {
  #region Application

  [UnmanagedCallersOnly(EntryPoint = "Dwarf_CreateApplication")]
  public static int Application_CreateApplication(IntPtr pAppName, int sizeX, int sizeY, int iVsync, int iFullscreen) {
    Logger.Info("Dwarf_CreateApplication called");

    var systems = SystemCreationFlags.Renderer3D |
              SystemCreationFlags.Physics3D |
              SystemCreationFlags.DirectionalLight |
              SystemCreationFlags.PointLights |
              SystemCreationFlags.Particles |
              SystemCreationFlags.Guizmos;
    var systemConfiguration = new SystemConfiguration() {
      PhysiscsBackend = Dwarf.Physics.Backends.BackendKind.Jolt
    };
    try {
      bool fullscreen = iFullscreen == 1;
      bool vsync = iVsync == 1;
      string appName = Marshal.PtrToStringAnsi(pAppName) ?? "";

      Logger.Info($"Creating Application: {appName}, Size: {sizeX}x{sizeY}, VSync: {vsync}, Fullscreen: {fullscreen}");

      _ = new Application(appName, new(sizeX, sizeY), systems, systemConfiguration, vsync, fullscreen, false);
    } catch (Exception ex) {
      Logger.Error(ex.Message);
      Logger.Error($"Stack Trace: {ex.StackTrace}");
      return -1;
    }
    return 0;
  }

  [UnmanagedCallersOnly(EntryPoint = "Dwarf_GetApplication")]
  public static IntPtr Application_GetApplication() {
    return (nint)Application.Instance;
  }

  [UnmanagedCallersOnly(EntryPoint = "Dwarf_SetUpdateCallback")]
  public static void Application_SetUpdateCallback() { }

  [UnmanagedCallersOnly(EntryPoint = "Dwarf_SetRenderCallback")]
  public static void Application_SetRenderCallback() { }

  [UnmanagedCallersOnly(EntryPoint = "Dwarf_SetGUICallback")]
  public static void Application_SetGUICallback() { }

  [UnmanagedCallersOnly(EntryPoint = "Dwarf_SetAppLoaderCallback")]
  public static void Application_SetAppLoaderCallback() { }

  [UnmanagedCallersOnly(EntryPoint = "Dwarf_SetOnLoadPrimaryCallback")]
  public static void Application_SetOnLoadPrimaryCallback() { }

  [UnmanagedCallersOnly(EntryPoint = "Dwarf_SetOnLoadCallback")]
  public static void Application_SetOnLoadCallback() { }

  [UnmanagedCallersOnly(EntryPoint = "Dwarf_CloseApp")]
  public static void Application_CloseApp() { }

  [UnmanagedCallersOnly(EntryPoint = "Dwarf_SetCurrentScene")]
  public unsafe static int Application_SetCurrentScene(IntPtr pScene) {
    var app = Application.Instance;
    if (app == null) return -1;

    if (pScene == IntPtr.Zero) return -1;

    GCHandle handle;
    try {
      handle = GCHandle.FromIntPtr(pScene);
    } catch {
      return -1;
    }

    if (!handle.IsAllocated || handle.Target is not Scene scene) return -1;

    app.SetCurrentScene(scene);
    return 0;
  }

  [UnmanagedCallersOnly(EntryPoint = "Dwarf_Run")]
  public static void Application_Run() {
    var app = Application.Instance;
    app.SetGUICallback(OnGui);
    app.UseFog = false;
    app.Run();
  }

  private static void OnGui() {
    var app = Application.Instance;

    ImOverlay.DrawOverlay("test", () => {
      ImGui.Text($"Frames: {Frames.GetFramesDelta()}");
      ImGui.Text($"Rendered 3d objects: {PerfMonitor.NumberOfObjectsRenderedIn3DRenderer}");
      ImGui.Text($"3D Texture binds: {PerfMonitor.TextureBindingsIn3DRenderer}");
      ImGui.Text($"3D Vertex binds: {PerfMonitor.VertexBindingsIn3DRenderer}");
      ImGui.Text($"3D CPU Update Time: {PerfMonitor.Render3DComputeTime}");

      ImGui.SliderFloat3("Fog", ref app.FogValue, 0f, 100);
      ImGui.SliderFloat4("Fog Color", ref app.FogColor, 0f, 1);
      ImGui.Checkbox("Use Fog", ref app.UseFog);
    });

    ImGui.Begin("Camera");

    ImGui.Text($"Position {CameraState.GetCameraEntity()?.TryGetComponent<Transform>()?.Position}");
    ImGui.Text($"Yaw {CameraState.GetCamera()?.Yaw}");
    ImGui.Text($"Pitch {CameraState.GetCamera()?.Pitch}");

    ImGui.End();
  }

  public static void Application_SetSkybox(bool value) {
    Application.Instance.UseSkybox = value;
  }

  #endregion

  #region Scene

  [UnmanagedCallersOnly(EntryPoint = "Dwarf_Scene_CreateScene")]
  public unsafe static IntPtr Scene_CreateScene() {
    var scene = new DefaultScene(Application.Instance);
    Application.Instance.SetCurrentScene(scene);
    return (nint)(&scene);

    // return IntPtr.Zero;
  }

  [UnmanagedCallersOnly(EntryPoint = "Dwarf_Scene_AddEntity")]
  public static int Scene_AddEntity(
    IntPtr pScene,
    IntPtr pEntity
  ) {
    unsafe {
      Scene* scene = (Scene*)pScene;
      Entity? entity = (Entity)(GCHandle.FromIntPtr(pEntity).Target ?? null!);

      if (entity == null) return -1;

      scene->AddEntity(entity);

      return 0;
    }
  }

  #endregion

  #region Entity Component System

  [UnmanagedCallersOnly(EntryPoint = "Dwarf_Entity_AddTransform")]
  public static void Entity_AddTransform(
    IntPtr pEntity,
    (float x, float y, float z) position,
    (float x, float y, float z) rotation,
    (float x, float y, float z) scale
  ) {
    unsafe {
      Entity* entity = (Entity*)pEntity;
      entity->AddTransform(
        new(position.x, position.y, position.z),
        new(rotation.x, rotation.y, rotation.z),
        new(scale.x, scale.y, scale.z)
      );
    }
  }

  [UnmanagedCallersOnly(EntryPoint = "Dwarf_Entity_AddMaterial")]
  public static void Entity_AddMaterial(
    IntPtr pEntity
  ) {
    unsafe {
      Entity* entity = (Entity*)pEntity;
      entity->AddMaterial();
    }
  }

  [UnmanagedCallersOnly(EntryPoint = "Dwarf_Entity_AddPrimitive")]
  public static void Entity_AddPrimitive(
    IntPtr pEntity,
    IntPtr pTexturePath,
    int primitiveType
  ) {
    unsafe {
      string texturePath = Marshal.PtrToStringAnsi(pTexturePath) ?? "";
      Entity* entity = (Entity*)pEntity;
      entity->AddPrimitive(texturePath, (PrimitiveType)primitiveType);
    }
  }

  public static void Entity_AddComponent(
    IntPtr pEntity,
    IntPtr pComponent
  ) {

  }

  #endregion
}


#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type