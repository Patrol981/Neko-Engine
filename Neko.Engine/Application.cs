using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Neko.AbstractionLayer;
using Neko.Audio;
using Neko.EntityComponentSystem;
using Neko.Extensions.Logging;
using Neko.Globals;
using Neko.Math;
using Neko.Rendering;
using Neko.Rendering.Lightning;
using Neko.Rendering.Renderer2D.Components;
using Neko.Rendering.Renderer2D.Models;
using Neko.Rendering.Renderer3D;
using Neko.Rendering.UI;
using Neko.Rendering.UI.DirectRPG;
using Neko.Utils;
using Neko.Vulkan;
using Neko.Windowing;
using ZLinq;
using Entity = Neko.EntityComponentSystem.Entity;

namespace Neko;

public partial class Application {
  public static Application Instance { get; private set; } = null!;
  public delegate void EventCallback();

  public IDevice Device { get; internal set; } = null!;
  public IWindow Window { get; } = null!;
  public IRenderer Renderer { get; } = null!;
  public TextureManager TextureManager => _textureManager;
  public nint Allocator { get; internal set; }
  public static Mutex Mutex { get; private set; } = new(false);
  public FrameInfo FrameInfo => _currentFrame;
  public DirectionalLight DirectionalLight = DirectionalLight.New();
  public ImGuiController GuiController { get; private set; } = null!;
  public SystemCollection Systems { get; } = null!;
  public IStorageCollection StorageCollection { get; private set; } = null!;
  public Scene CurrentScene { get; private set; } = null!;
  public bool UseImGui { get; } = true;
  public unsafe GlobalUniformBufferObject GlobalUbo => *_ubo;

  public void SetUpdateCallback(EventCallback eventCallback) {
    _onUpdate = eventCallback;
  }

  public void SetRenderCallback(EventCallback eventCallback) {
    _onRender = eventCallback;
  }

  public void SetGUICallback(EventCallback eventCallback) {
    _onGUI = eventCallback;
  }

  public void SetAppLoaderCallback(EventCallback eventCallback) {
    _onAppLoading = eventCallback;
  }

  public void SetOnLoadPrimaryCallback(EventCallback eventCallback) {
    _onLoadPrimaryResources = eventCallback;
  }

  public void SetOnLoadCallback(EventCallback eventCallback) {
    _onLoad = eventCallback;
  }

  public IPipelineConfigInfo CurrentPipelineConfig = null!;

  private EventCallback? _onUpdate;
  private EventCallback? _onRender;
  private EventCallback? _onGUI;
  private EventCallback? _onAppLoading;
  private EventCallback? _onLoad;
  private EventCallback? _onLoadPrimaryResources;
  private TextureManager _textureManager = null!;

  internal Entity? CameraEntity = default!;
  internal Camera CameraComponent = default!;

  // ubos
  private IDescriptorPool _globalPool = null!;
  private Dictionary<string, IDescriptorSetLayout> _descriptorSetLayouts = [];

  private readonly SystemCreationFlags _systemCreationFlags;
  public readonly SystemConfiguration SystemConfiguration;

  private Thread? _renderThread;
  private bool _renderShouldClose = false;
  private bool _newSceneShouldLoad = false;
  private bool _appExitRequest = false;

  private Skybox? _skybox = null;
  public bool UseSkybox = true;

  // private GlobalUniformBufferObject _ubo;
  private readonly unsafe GlobalUniformBufferObject* _ubo =
    (GlobalUniformBufferObject*)Marshal.AllocHGlobal(Unsafe.SizeOf<GlobalUniformBufferObject>());

  private FrameInfo _currentFrame = new();

  public RenderAPI CurrentAPI { get; private set; }
  public bool VSync { get; init; } = false;
  public bool Fullscreen { get; init; } = false;
  public bool Debug { get; init; } = false;
  public static ApplicationType ApplicationMode { get; private set; } = ApplicationType.Default;
  public readonly Lock ApplicationLock = new();

  public const int ThreadTimeoutTimeMS = 1000;

  public Vector3 FogValue = Vector3.UnitX;
  public Vector4 FogColor = Vector4.One;
  public bool UseFog = true;

  public Application(
    string appName = "Neko App",
    Vector2I windowSize = default!,
    SystemCreationFlags systemCreationFlags = SystemCreationFlags.Renderer3D,
    SystemConfiguration? systemConfiguration = default,
    bool vsync = false,
    bool fullscreen = false,
    bool debugMode = true
  ) {
    Instance = this;
    CurrentAPI = RenderAPI.Vulkan;
    VSync = vsync;
    Fullscreen = fullscreen;
    Debug = debugMode;

    systemConfiguration ??= SystemConfiguration.Default;
    SystemConfiguration = systemConfiguration;

    ApplicationMode = SystemConfiguration.ApplicationType;
    Logger.Info($"[APP MODE] {ApplicationMode}");

    _ = new AudioSystem();

    switch (ApplicationMode) {
      case ApplicationType.Default:
        VulkanDevice.s_EnableValidationLayers = debugMode;

        windowSize ??= new(1200, 900);
        Window = new Window();
        Window.Init(appName, Fullscreen, windowSize.X, windowSize.Y, debugMode);

        Device = new VulkanDevice(Window);
        ResourceInitializer.InitAllocator(Device, out var allocator);
        Allocator = allocator;

        Renderer = RendererFactory.CreateAPIRenderer(this);
        StorageCollection = ApplicationFactory.CreateStorageCollection(Allocator, Device);
        _textureManager = new(Allocator, Device);

        _onAppLoading = () => {
          DirectRPG.BeginCanvas();
          DirectRPG.CanvasText("Loading...");
          DirectRPG.EndCanvas();
        };

        break;
      case ApplicationType.Headless:
        break;
    }

    Systems = new SystemCollection();
    _systemCreationFlags = systemCreationFlags;
    Time.Init();
  }

  public async void Run() {
    Logger.Info("[APPLICATION] Application started");

    _onLoadPrimaryResources?.Invoke();

    if (UseImGui) {
      GuiController = new(Allocator, Device, Renderer);
      await GuiController.Init((int)Window.Extent.Width, (int)Window.Extent.Height);
    }

    _renderThread = new Thread(LoaderLoop) {
      Name = "App Loading Frontend Thread"
    };
    _renderThread.Start();

    Application.Mutex.WaitOne();
    await Init();
    Application.Mutex.ReleaseMutex();

    _renderShouldClose = true;
    Logger.Info("Waiting for renderer to close...");
    while (_renderShouldClose) { Console.Write(""); }
    _renderThread?.Join();

    Logger.Info("Waiting for render thread to close...");
    while (_renderThread!.IsAlive) {
    }

    _renderShouldClose = false;
    Logger.Info("[APPLICATION] Application loaded. Starting render thread.");
    _renderThread = new Thread(RenderLoop) {
      Name = "Render Thread"
    };
    _renderThread.Start();

    Logger.Info("[APPLICATION] Application loaded. Starting render thread.");

    while (!Window.ShouldClose) {
      Input.ScrollDelta = 0.0f;
      Time.Tick();
      Window.PollEvents();
      if (!Window.IsMinimalized) {
        Window.Show();
      } else {
        Window.WaitEvents();
      }

      PerformCalculations();

      if (!Scripts.IsEmpty) {
        MasterFixedUpdate(Scripts.Values.ToArray());
        _onUpdate?.Invoke();
        MasterUpdate(Scripts.Values.ToArray());
      }

      if (_newSceneShouldLoad) {
        SceneLoadReactor();
      }

      if (_appExitRequest) {
        HandleExit();
      }

      // GC.Collect(2, GCCollectionMode.Optimized, false);
    }

    Mutex.WaitOne();
    try {
      Device.WaitDevice();
    } finally {
      Mutex.ReleaseMutex();
    }

    _renderShouldClose = true;

    if (_renderThread != null && _renderThread.IsAlive)
      _renderThread?.Join();

    Cleanup();
  }

  public async Task RunHeadless() {
    Logger.Info("[APPLICATION] Application started in headless mode");

    await SetupScene();


    Systems.SetupHeadless(this, _systemCreationFlags, SystemConfiguration);
    var closeRequest = false;

    Logger.NoLabel(HeadlessUI.NekoWelcome, ConsoleColor.Green);

    while (!closeRequest) {
      if (Console.KeyAvailable) {
        var keyInfo = Console.ReadKey(true);
        switch (keyInfo.Key) {
          case ConsoleKey.Q:
            closeRequest = true;
            break;
          case ConsoleKey.L:
            foreach (var ent in Entities) {
              Logger.Info(ent.Name);
            }
            break;
          case ConsoleKey.F1:
            Logger.NoLabel(HeadlessUI.CommandList);
            break;
          case ConsoleKey.S:
            break;
          case ConsoleKey.T:
            var threads = Process.GetCurrentProcess().Threads;
            break;
          default:
            break;
        }
      }

      Time.Tick();
      PerformCalculations();

      if (!Scripts.IsEmpty) {
        MasterFixedUpdate(Scripts.Values.ToArray());
        _onUpdate?.Invoke();
        MasterUpdate(Scripts.Values.ToArray());
      }
    }

    Logger.Info("[APPLICATION] Closing App");

    Cleanup();
  }

  public void SetCamera(Entity camera) {
    CameraEntity = camera;
  }
  #region RESOURCES
  private unsafe Task InitResources() {
    ResourceInitializer.InitResources(Device, Renderer, StorageCollection, ref _globalPool, ref _descriptorSetLayouts);

    Mutex.WaitOne();
    Systems.Setup(
      this,
      _systemCreationFlags,
      SystemConfiguration,
      Allocator,
      Device,
      Renderer,
      _descriptorSetLayouts,
      null!,
      ref _textureManager
    );

    ResourceInitializer.SetupResources(
      Device,
      Renderer,
      Systems,
      StorageCollection,
      ref _globalPool,
      ref _descriptorSetLayouts,
      UseSkybox
    );
    Mutex.ReleaseMutex();

    MasterAwake(Scripts.Values.ToArray());
    _onLoad?.Invoke();
    MasterStart(Scripts.Values.ToArray());

    return Task.CompletedTask;
  }

  private async Task<Task> Init() {
    var tasks = new Task[] {
      await SetupScene(),
      InitResources()
    };

    await Task.WhenAll(tasks);

    return Task.CompletedTask;
  }

  [Obsolete]
  private async Task<Task> LoadTextures() {
    if (CurrentScene == null) return Task.CompletedTask;
    CurrentScene.LoadTextures();
    var paths = CurrentScene.GetTexturePaths();

    var startTime = DateTime.UtcNow;

    List<List<ITexture>> textures = [];
    for (int i = 0; i < paths.Count; i++) {
      var t = await TextureManager.AddTextures(Allocator, Device, [.. paths[i]]);
      textures.Add([.. t]);
    }

    for (int i = 0; i < paths.Count; i++) {
      _textureManager.AddRangeLocal([.. textures[i]]);
    }

    var endTime = DateTime.Now;
    Logger.Info($"[TEXTURE] Load Time {endTime - startTime}");


    return Task.CompletedTask;
  }

  private Task LoadEntities() {
    if (CurrentScene == null) return Task.CompletedTask;
    var startTime = DateTime.UtcNow;

    Mutex.WaitOne();
    CurrentScene.LoadEntities();
    foreach (var entity in CurrentScene.GetEntities()) {
      Entities.Add(entity);
    }
    Mutex.ReleaseMutex();

    var endTime = DateTime.Now;
    Logger.Info($"[Entities] Load Time {endTime - startTime}");
    return Task.CompletedTask;
  }

  #endregion RESOURCES
  #region ENTITY_FLOW
  private static void MasterAwake(ReadOnlySpan<NekoScript> scripts) {
#if RUNTIME
    var sc = scripts.ToArray();
    // Parallel.ForEach(ents, (entity) => {
    //   entity.Awake();
    // });
    foreach (var s in sc) {
      s.Awake();
    }
#endif
  }

  private static void MasterStart(ReadOnlySpan<NekoScript> scripts) {
#if RUNTIME
    var sc = scripts.ToArray();
    // Parallel.ForEach(ents, (entity) => {
    //   entity.Start();
    // });
    foreach (var s in sc) {
      s.Start();
    }
#endif
  }

  private static void MasterUpdate(NekoScript[] scripts) {
#if RUNTIME
    Parallel.For(0, scripts.Length, i => { scripts[i].Update(); });
#endif
  }

  private static void MasterFixedUpdate(ReadOnlySpan<NekoScript> scripts) {
#if RUNTIME
    for (short i = 0; i < scripts.Length; i++) {
      scripts[i].FixedUpdate();
    }
#endif
  }

  private static void MasterRenderUpdate(ReadOnlySpan<NekoScript> entities) {
#if RUNTIME
    for (short i = 0; i < entities.Length; i++) {
      entities[i].RenderUpdate();
    }
#endif
  }

  #endregion ENTITY_FLOW
  #region APPLICATION_LOOP
  private unsafe void Render(ThreadInfo threadInfo) {
    Time.RenderTick();
    if (Window.IsMinimalized) return;

    Systems.ValidateSystems(
        this,
        Allocator, Device, Renderer,
        _descriptorSetLayouts,
        CurrentPipelineConfig,
        ref _textureManager
      );

    float aspect = Renderer.AspectRatio;
    var cameraAsppect = CameraEntity?.GetCamera()?.Aspect;
    if (aspect != cameraAsppect && cameraAsppect != null) {
      CameraEntity!.GetCamera()!.Aspect = aspect;
      switch (CameraEntity!.GetCamera()?.CameraType) {
        case CameraType.Perspective:
          CameraEntity!.GetCamera()?.SetPerspectiveProjection(0.1f, 100f);
          break;
        case CameraType.Orthographic:
          CameraEntity!.GetCamera()?.SetOrthograpicProjection();
          break;
        default:
          break;
      }
    }

    var camera = CameraEntity?.GetCamera();
    nint commandBuffer = IntPtr.Zero;

    if (camera != null) {
      commandBuffer = Renderer.BeginFrame();
    }

    if (commandBuffer != IntPtr.Zero && camera != null) {
      var entities = Entities.AsValueEnumerable().ToArray();

      int frameIndex = Renderer.FrameIndex;
      _currentFrame.Camera = camera;
      _currentFrame.CommandBuffer = commandBuffer;
      _currentFrame.FrameIndex = frameIndex;
      _currentFrame.GlobalDescriptorSet = StorageCollection.GetDescriptor("GlobalStorage", frameIndex);
      _currentFrame.PointLightsDescriptorSet = StorageCollection.GetDescriptor("PointStorage", frameIndex);
      _currentFrame.StaticObjectDataDescriptorSet = StorageCollection.GetDescriptor("StaticObjectStorage", frameIndex);
      _currentFrame.SkinnedObjectDataDescriptorSet = StorageCollection.GetDescriptor("SkinnedObjectStorage", frameIndex);
      _currentFrame.CustomShaderObjectDataDescriptorSet = StorageCollection.GetDescriptor("CustomShaderObjectStorage", frameIndex);
      _currentFrame.SpriteDataDescriptorSet = StorageCollection.GetDescriptor("SpriteStorage", frameIndex);
      _currentFrame.CustomSpriteDataDescriptorSet = StorageCollection.GetDescriptor("CustomSpriteStorage", frameIndex);
      _currentFrame.JointsBufferDescriptorSet = StorageCollection.GetDescriptor("JointsStorage", frameIndex);
      _currentFrame.TextureManager = _textureManager;
      _currentFrame.ImportantEntity = entities.Where(x => x.IsImportant).FirstOrDefault() ?? null!;

      _ubo->Projection = camera?.GetProjectionMatrix() ?? Matrix4x4.Identity;
      _ubo->View = camera?.GetViewMatrix() ?? Matrix4x4.Identity;
      _ubo->CameraPosition = CameraEntity?.GetTransform()?.Position ?? Vector3.Zero;
      _ubo->Fov = 60;
      _ubo->ImportantEntityPosition = _currentFrame.ImportantEntity?.GetTransform()?.Position ?? Vector3.Zero;
      _ubo->ImportantEntityPosition.Z += 2.0f;
      _ubo->ImportantEntityDirection = _currentFrame.ImportantEntity?.GetTransform()?.Forward() ?? Vector3.Zero;
      _ubo->HasImportantEntity = _currentFrame.ImportantEntity != null ? 1 : 0;
      // _ubo->Fog = FogValue;
      _ubo->Fog = new(FogValue.X, Window.Extent.Width, Window.Extent.Height);
      _ubo->FogColor = FogColor;
      _ubo->UseFog = UseFog ? 1 : 0;
      // _ubo->ImportantEntityPosition = new(6, 9);
      _ubo->ScreenSize = new(Window.Extent.Width, Window.Extent.Height);
      _ubo->HatchScale = Render3DSystem.HatchScale;
      _ubo->DeltaTime = Time.DeltaTime;

      _ubo->DirectionalLight = DirectionalLight;

      Systems.UpdateSystems(this, FrameInfo, _ubo);

      StorageCollection.WriteBuffer(
        "GlobalStorage",
        frameIndex,
        (nint)_ubo,
        (ulong)Unsafe.SizeOf<GlobalUniformBufferObject>()
      );

      Renderer.BeginRendering(commandBuffer);

      _onRender?.Invoke();
      _skybox?.Render(_currentFrame);

      if (Debug) {
        TracyWrapper.Profiler.PushProfileZone("Systems", System.Drawing.Color.AliceBlue);
      }

      Systems.RenderSystems(this, _currentFrame);

      if (Debug) {
        TracyWrapper.Profiler.PopProfileZone();
      }

      Renderer.EndRendering(commandBuffer);
      Renderer.BeginPostProcess(commandBuffer);

      if (UseImGui) {
        GuiController.Update(Time.StopwatchDelta);
      }

      Systems.RenderSystems2(this, _currentFrame);
      MasterRenderUpdate(Scripts.Values.ToArray());
      _onGUI?.Invoke();
      if (UseImGui) {
        GuiController.Render(_currentFrame);
      }

      Renderer.EndPostProcess(commandBuffer);

      Renderer.EndFrame();

      Systems.CheckStorageSizes(this, FrameInfo, _descriptorSetLayouts);

      while (_reloadQueue.Count > 0) {
        var item = _reloadQueue.Dequeue();
        item.Dispose();
        item.Init(Meshes, item.AABBFilter);
      }
    }

    Collect();

    if (_entitiesQueue.Count > 0) {
      Mutex.WaitOne();
      while (_entitiesQueue.Count > 0) {
        Entities.Add(_entitiesQueue.Dequeue());
      }
      Mutex.ReleaseMutex();
    }
  }

  internal void RenderLoader() {
    var commandBuffer = Renderer.BeginFrame();
    if (commandBuffer != IntPtr.Zero) {
      int frameIndex = Renderer.FrameIndex;

      _currentFrame.CommandBuffer = commandBuffer;
      _currentFrame.FrameIndex = frameIndex;

      Renderer.BeginRendering(commandBuffer);
      if (UseImGui) {
        GuiController.Update(Time.StopwatchDelta);
        _onAppLoading?.Invoke();
        GuiController.Render(_currentFrame);
      }
      Renderer.EndRendering(commandBuffer);

      Renderer.EndFrame();
    }
  }

  private void PerformCalculations() {
    Systems?.UpdateCalculationSystems(this);
  }

  internal unsafe void LoaderLoop() {
    var pool = Device.CreateCommandPool();
    var threadInfo = new ThreadInfo() {
      CommandPool = pool,
    };

    Renderer.CreateCommandBuffers(threadInfo.CommandPool, CommandBufferLevel.Primary);

    while (!_renderShouldClose) {
      RenderLoader();
    }

    Device.WaitQueue();
    Device.WaitDevice();

    Device.DisposeCommandPool(threadInfo.CommandPool);

    _renderShouldClose = false;
  }

  internal unsafe void RenderLoop() {
    Mutex.WaitOne();
    var pool = Device.CreateCommandPool();
    var threadInfo = new ThreadInfo() {
      CommandPool = pool,
    };

    Renderer.CreateCommandBuffers(threadInfo.CommandPool, CommandBufferLevel.Primary);
    Mutex.ReleaseMutex();

    if (Debug)
      TracyWrapper.Profiler.InitThread();

    while (!_renderShouldClose) {
      if (Window.IsMinimalized) continue;

      Render(threadInfo);

      GC.Collect(2, GCCollectionMode.Optimized, false);
    }

    Logger.Info("[RENDER LOOP] Closing Renderer");

    Device.WaitQueue();
    Device.WaitDevice();

    Device.DisposeCommandPool(threadInfo.CommandPool);

    _renderShouldClose = false;
  }
  #endregion APPLICATION_LOOP

  private void Cleanup() {
    _skybox?.Dispose();
    GuiController?.Dispose();

    Span<Entity> entities = Entities.ToArray();
    for (int i = 0; i < entities.Length; i++) {
      entities[i].Dispose(this);
    }

    _textureManager?.Dispose();
    foreach (var layout in _descriptorSetLayouts) {
      layout.Value.Dispose();
    }
    _globalPool?.Dispose();
    unsafe {
      MemoryUtils.FreeIntPtr<GlobalUniformBufferObject>((nint)_ubo);
    }
    StorageCollection?.Dispose();
    Systems?.Dispose();
    Renderer?.Dispose();
    Window?.Dispose();
    if (Allocator != IntPtr.Zero) {
      Logger.Info("[ALLOCATION] Disposing Allocator");
      ResourceInitializer.DestroyAllocator(Device, Allocator);
    }
    Device?.Dispose();
  }

  public void CloseApp() {
    _appExitRequest = true;
  }

  private async void HandleExit() {
    await Coroutines.CoroutineRunner.Instance.StopAllCoroutines();

    Guizmos.Clear();
    Guizmos.Free();
    foreach (var e in Entities) {
      e.CanBeDisposed = true;
    }

    Logger.Info($"Waiting for entities to dispose... [{Entities.Count()}]");
    if (Entities.Count() > 0) {
      return;
    }

    if (!_renderShouldClose) {
      _renderShouldClose = true;
      return;
    }

    Logger.Info("Waiting for render process to close...");
    _renderThread?.Join();

    Logger.Info("Waiting for render thread to close...");
    while (_renderThread!.IsAlive) {
    }

    Systems.PhysicsSystem?.Dispose();

    System.Environment.Exit(1);
  }

#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
  public unsafe static explicit operator nint(Application app) {
    return (nint)(&app);
  }
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
}