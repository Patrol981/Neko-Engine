using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Dwarf.AbstractionLayer;
using Dwarf.EntityComponentSystem;
using Dwarf.Extensions.Logging;
using Dwarf.Globals;
using Dwarf.Math;
using Dwarf.Rendering;
using Dwarf.Rendering.Lightning;
using Dwarf.Rendering.Renderer3D;
using Dwarf.Rendering.UI;
using Dwarf.Rendering.UI.DirectRPG;
using Dwarf.Utils;
using Dwarf.Vulkan;
using Dwarf.Windowing;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vma;
// using static Dwarf.GLFW.GLFW;
using static Vortice.Vulkan.Vulkan;

namespace Dwarf;

public class Application {
  public static Application Instance { get; private set; } = null!;
  public delegate void EventCallback();

  public IDevice Device { get; internal set; } = null!;
  public IWindow Window { get; } = null!;
  public IRenderer Renderer { get; } = null!;
  public TextureManager TextureManager => _textureManager;
  public nint Allocator { get; internal set; }
  public Mutex Mutex { get; private set; }
  public FrameInfo FrameInfo => _currentFrame;
  public DirectionalLight DirectionalLight = DirectionalLight.New();
  public ImGuiController GuiController { get; private set; } = null!;
  public SystemCollection Systems { get; } = null!;
  public IStorageCollection StorageCollection { get; private set; } = null!;
  public Scene CurrentScene { get; private set; } = null!;
  public bool UseImGui { get; } = true;
  public unsafe GlobalUniformBufferObject GlobalUbo => *_ubo;

  public const int MAX_POINT_LIGHTS_COUNT = 128;

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

  public VkPipelineConfigInfo CurrentPipelineConfig = new VkPipelineConfigInfo();

  private EventCallback? _onUpdate;
  private EventCallback? _onRender;
  private EventCallback? _onGUI;
  private EventCallback? _onAppLoading;
  private EventCallback? _onLoad;
  private EventCallback? _onLoadPrimaryResources;
  private TextureManager _textureManager = null!;

  private List<Entity> _entities = [];
  private readonly Queue<Entity> _entitiesQueue = new();
  private readonly Queue<MeshRenderer> _reloadQueue = new();
  public readonly object EntitiesLock = new object();

  private Entity _camera = new();

  // ubos
  private IDescriptorPool _globalPool = null!;
  private Dictionary<string, IDescriptorSetLayout> _descriptorSetLayouts = [];

  private readonly SystemCreationFlags _systemCreationFlags;
  private readonly SystemConfiguration _systemConfiguration;

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
  public readonly object ApplicationLock = new object();

  public const int ThreadTimeoutTimeMS = 1000;

  public Vector3 FogValue = Vector3.UnitX;
  public Vector4 FogColor = Vector4.One;
  public bool UseFog = true;

  public Application(
    string appName = "Dwarf App",
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

    VulkanDevice.s_EnableValidationLayers = debugMode;

    windowSize ??= new(1200, 900);

    Window = new Window();
    Window.Init(appName, Fullscreen, windowSize.X, windowSize.Y, debugMode);

    Device = new VulkanDevice(Window);

    ResourceInitializer.VkInitAllocator(Device, out var allocator);
    Allocator = allocator.Handle;

    // Renderer = new Renderer(Window, Device);
    Renderer = new VkDynamicRenderer(this);
    Systems = new SystemCollection();
    StorageCollection = new VkStorageCollection(Allocator, (VulkanDevice)Device);

    _textureManager = new(Allocator, Device);
    _systemCreationFlags = systemCreationFlags;

    systemConfiguration ??= SystemConfiguration.Default;
    _systemConfiguration = systemConfiguration;

    Mutex = new Mutex(false);

    _onAppLoading = () => {
      DirectRPG.BeginCanvas();
      DirectRPG.CanvasText("Loading...");
      DirectRPG.EndCanvas();
    };

    Time.Init();
  }

  public void SetCurrentScene(Scene scene) {
    CurrentScene = scene;
  }

  public void LoadScene(Scene scene) {
    SetCurrentScene(scene);
    _newSceneShouldLoad = true;
  }
  private async void SceneLoadReactor() {
    Mutex.WaitOne();
    Device.WaitDevice();
    Device.WaitQueue();
    Mutex.ReleaseMutex();

    await Coroutines.CoroutineRunner.Instance.StopAllCoroutines();

    Guizmos.Clear();
    Guizmos.Free();
    foreach (var e in _entities) {
      e.CanBeDisposed = true;
    }

    // Mutex.WaitOne();
    // while (_entities.Count > 0) {
    //   Logger.Info($"Waiting for entities to dispose... [{_entities.Count}]");
    //   Collect();
    //   _entities.Clear();
    //   _entities = [];
    // }

    Logger.Info($"Waiting for entities to dispose... [{_entities.Count}]");
    if (_entities.Count > 0) {
      return;
    }

    // Mutex.ReleaseMutex();

    _renderShouldClose = true;
    // if (!_renderShouldClose) {

    //   return;
    // }

    // while (_renderShouldClose) {

    // }

    Logger.Info("Waiting for render process to close...");
    _renderThread?.Join();

    Logger.Info("Waiting for render thread to close...");
    while (_renderThread!.IsAlive) {
    }

    _renderThread = new Thread(LoaderLoop) {
      Name = "App Loading Frontend Thread"
    };
    _renderThread.Start();

    Mutex.WaitOne();
    Systems.Dispose();
    StorageCollection.Dispose();
    foreach (var layout in _descriptorSetLayouts) {
      layout.Value.Dispose();
    }
    _descriptorSetLayouts = [];
    _globalPool.Dispose();
    _globalPool = null!;
    _skybox?.Dispose();
    _textureManager.DisposeLocal();

    StorageCollection = new VkStorageCollection(Allocator, (VulkanDevice)Device);
    Mutex.ReleaseMutex();
    await Init();

    _renderShouldClose = true;
    Logger.Info("[Scene Reactor Finalizer] Waiting for loading render process to close...");
    // while (_renderShouldClose) {

    // }
    while (_renderThread.IsAlive) {

    }

    _newSceneShouldLoad = false;
    _renderThread.Join();
    _renderThread = new Thread(RenderLoop) {
      Name = "Render Thread"
    };
    _renderThread.Start();

  }

  private async Task<Task> SetupScene() {
    if (CurrentScene == null) return Task.CompletedTask;

    await LoadTextures();
    await LoadEntities();

    Logger.Info($"Loaded entities: {_entities.Count}");
    Logger.Info($"Loaded textures: {_textureManager.PerSceneLoadedTextures.Count}");

    return Task.CompletedTask;
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

    await Init();

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

      var cp = _entities.ToArray();
      var updatable = cp.Where(x => x.CanBeDisposed == false).ToArray();
      MasterFixedUpdate(updatable.GetScriptsAsSpan());
      _onUpdate?.Invoke();
      MasterUpdate(updatable.GetScriptsAsArray());

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
      var result = vkDeviceWaitIdle(Device.LogicalDevice);
      if (result != VkResult.Success) {
        Logger.Error(result.ToString());
      }
    } finally {
      Mutex.ReleaseMutex();
    }

    _renderShouldClose = true;

    if (_renderThread != null && _renderThread.IsAlive)
      _renderThread?.Join();

    Cleanup();
  }

  public void SetCamera(Entity camera) {
    _camera = camera;
  }
  #region RESOURCES
  private unsafe Task InitResources() {
    ResourceInitializer.VkInitResources(Device, Renderer, StorageCollection, ref _globalPool, ref _descriptorSetLayouts);

    Mutex.WaitOne();
    // SetupSystems(_systemCreationFlags, Device, Renderer, _globalSetLayout, null!);
    Systems.Setup(
      this,
      _systemCreationFlags,
      _systemConfiguration,
      Allocator,
      Device,
      Renderer,
      _descriptorSetLayouts,
      null!,
      ref _textureManager
    );

    ResourceInitializer.VkSetupResources(Device, Renderer, Systems, StorageCollection, ref _globalPool, ref _descriptorSetLayouts, UseSkybox);
    Mutex.ReleaseMutex();
    // _imguiController.InitResources(_renderer.GetSwapchainRenderPass(), _device.GraphicsQueue, "imgui_vertex", "imgui_fragment");

    MasterAwake(_entities.GetScripts());
    _onLoad?.Invoke();
    MasterStart(_entities.GetScripts());

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
    _entities.AddRange(CurrentScene.GetEntities());
    Mutex.ReleaseMutex();

    var endTime = DateTime.Now;
    Logger.Info($"[Entities] Load Time {endTime - startTime}");
    return Task.CompletedTask;
  }

  #endregion RESOURCES
  #region ENTITY_FLOW
  private static void MasterAwake(ReadOnlySpan<DwarfScript> entities) {
#if RUNTIME
    var ents = entities.ToArray();
    Parallel.ForEach(ents, (entity) => {
      entity.Awake();
    });
#endif
  }

  private static void MasterStart(ReadOnlySpan<DwarfScript> entities) {
#if RUNTIME
    var ents = entities.ToArray();
    Parallel.ForEach(ents, (entity) => {
      entity.Start();
    });
#endif
  }

  private static void MasterUpdate(DwarfScript[] entities) {
#if RUNTIME
    Parallel.For(0, entities.Length, i => { entities[i].Update(); });
    // for (short i = 0; i < entities.Length; i++) {
    //   entities[i].Update();
    // }
#endif
  }

  private static void MasterFixedUpdate(ReadOnlySpan<DwarfScript> entities) {
#if RUNTIME
    for (short i = 0; i < entities.Length; i++) {
      entities[i].FixedUpdate();
    }
#endif
  }

  private static void MasterRenderUpdate(ReadOnlySpan<DwarfScript> entities) {
#if RUNTIME
    for (short i = 0; i < entities.Length; i++) {
      entities[i].RenderUpdate();
    }
#endif
  }

  public void AddEntity(Entity entity, bool fenced = false) {
    Mutex.WaitOne();
    // lock (EntitiesLock) {

    // }
    MasterAwake(new[] { entity }.GetScriptsAsSpan());
    MasterStart(new[] { entity }.GetScriptsAsSpan());
    if (fenced) {
      var fence = Device.CreateFence(FenceCreateFlags.Signaled);
      Device.WaitFence(fence, true);
    }
    _entitiesQueue.Enqueue(entity);
    Mutex.ReleaseMutex();
  }

  public void AddEntities(Entity[] entities) {
    foreach (var entity in entities) {
      AddEntity(entity);
    }
  }

  public List<Entity> GetEntities() {
    lock (EntitiesLock) {
      return _entities;
    }
  }

  public Entity? GetEntity(Guid entitiyId) {
    lock (EntitiesLock) {
      return _entities.Where(x => x.EntityID == entitiyId).First();
    }
  }

  public void RemoveEntityAt(int index) {
    lock (EntitiesLock) {
      Device.WaitDevice();
      Device.WaitQueue();
      _entities.RemoveAt(index);
    }
  }

  public void RemoveEntity(Entity entity) {
    lock (EntitiesLock) {
      Device.WaitDevice();
      Device.WaitQueue();
      _entities.Remove(entity);
    }
  }

  public void RemoveEntity(Guid id) {
    lock (EntitiesLock) {
      if (_entities.Count == 0) return;
      var target = _entities.Where((x) => x.EntityID == id).FirstOrDefault();
      if (target == null) return;
      Device.WaitDevice();
      Device.WaitQueue();
      _entities.Remove(target);
    }
  }

  public void DestroyEntity(Entity entity) {
    lock (EntitiesLock) {
      entity.CanBeDisposed = true;
    }
  }

  public void RemoveEntityRange(int index, int count) {
    lock (EntitiesLock) {
      _entities.RemoveRange(index, count);
    }
  }

  public void AddModelToReloadQueue(MeshRenderer meshRenderer) {
    _reloadQueue.Enqueue(meshRenderer);
  }
  #endregion ENTITY_FLOW
  #region APPLICATION_LOOP
  private unsafe void Render(ThreadInfo threadInfo) {
    // Time.Tick();
    // Logger.Info("TICK");

    Time.RenderTick();
    if (Window.IsMinimalized) return;

    Systems.ValidateSystems(
        _entities.ToArray(),
        Allocator, Device, Renderer,
        _descriptorSetLayouts,
        CurrentPipelineConfig,
        ref _textureManager
      );

    float aspect = Renderer.AspectRatio;
    var cameraAsppect = _camera.TryGetComponent<Camera>()?.Aspect;
    if (aspect != cameraAsppect && cameraAsppect != null) {
      _camera.GetComponent<Camera>().Aspect = aspect;
      switch (_camera.GetComponent<Camera>().CameraType) {
        case CameraType.Perspective:
          _camera.GetComponent<Camera>()?.SetPerspectiveProjection(0.1f, 100f);
          break;
        case CameraType.Orthographic:
          _camera.GetComponent<Camera>()?.SetOrthograpicProjection();
          break;
        default:
          break;
      }
    }

    var camera = _camera.TryGetComponent<Camera>();
    VkCommandBuffer commandBuffer = VkCommandBuffer.Null;

    if (camera != null) {
      commandBuffer = Renderer.BeginFrame();
    }

    if (commandBuffer != VkCommandBuffer.Null && camera != null) {
      int frameIndex = Renderer.FrameIndex;
      _currentFrame.Camera = camera;
      _currentFrame.CommandBuffer = commandBuffer;
      _currentFrame.FrameIndex = frameIndex;
      _currentFrame.GlobalDescriptorSet = StorageCollection.GetDescriptor("GlobalStorage", frameIndex);
      _currentFrame.PointLightsDescriptorSet = StorageCollection.GetDescriptor("PointStorage", frameIndex);
      _currentFrame.ObjectDataDescriptorSet = StorageCollection.GetDescriptor("ObjectStorage", frameIndex);
      _currentFrame.JointsBufferDescriptorSet = StorageCollection.GetDescriptor("JointsStorage", frameIndex);
      _currentFrame.TextureManager = _textureManager;
      _currentFrame.ImportantEntity = _entities.Where(x => x.IsImportant).FirstOrDefault() ?? null!;

      _ubo->Projection = _camera.TryGetComponent<Camera>()?.GetProjectionMatrix() ?? Matrix4x4.Identity;
      _ubo->View = _camera.TryGetComponent<Camera>()?.GetViewMatrix() ?? Matrix4x4.Identity;
      _ubo->CameraPosition = _camera.TryGetComponent<Transform>()?.Position ?? Vector3.Zero;
      _ubo->Fov = 60;
      _ubo->ImportantEntityPosition = _currentFrame.ImportantEntity?.TryGetComponent<Transform>()?.Position ?? Vector3.Zero;
      _ubo->ImportantEntityPosition.Z += 0.5f;
      _ubo->ImportantEntityDirection = _currentFrame.ImportantEntity?.TryGetComponent<Transform>()?.Forward ?? Vector3.Zero;
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

      ReadOnlySpan<Entity> entities = _entities.ToArray();

      if (Systems.PointLightSystem != null) {
        Systems.PointLightSystem.Update(entities, out var pointLights);
        if (pointLights.Length > 1) {
          _ubo->PointLightsLength = pointLights.Length;
          fixed (PointLight* pPointLights = pointLights) {
            StorageCollection.WriteBuffer(
              "PointStorage",
              frameIndex,
              (nint)pPointLights,
              (ulong)Unsafe.SizeOf<PointLight>() * MAX_POINT_LIGHTS_COUNT
            );
          }
        } else {
          _ubo->PointLightsLength = 0;
        }
      }

      var i3D = _entities.ToArray().DistinctI3D();

      if (Systems.Render3DSystem != null) {
        Systems.Render3DSystem.Update(
          i3D,
          out var objectData,
          out var skinnedObjects,
          out var flatJoints
        );
        fixed (ObjectData* pObjectData = objectData) {
          StorageCollection.WriteBuffer(
            "ObjectStorage",
            frameIndex,
            (nint)pObjectData,
            (ulong)Unsafe.SizeOf<ObjectData>() * (ulong)objectData.Length
          );
        }

        ReadOnlySpan<Matrix4x4> flatArray = [.. flatJoints];
        fixed (Matrix4x4* pMatrices = flatArray) {
          StorageCollection.WriteBuffer(
            "JointsStorage",
            frameIndex,
            (nint)pMatrices,
            (ulong)Unsafe.SizeOf<Matrix4x4>() * (ulong)flatArray.Length
          );
        }
      }

      Systems.ShadowRenderSystem?.Update(i3D);

      StorageCollection.WriteBuffer(
        "GlobalStorage",
        frameIndex,
        (nint)_ubo,
        (ulong)Unsafe.SizeOf<GlobalUniformBufferObject>()
      );

      Renderer.BeginRendering(commandBuffer);

      _onRender?.Invoke();
      _skybox?.Render(_currentFrame);
      Entity[] toUpdate = [.. _entities];
      Systems.UpdateSystems(toUpdate, _currentFrame);

      // Renderer.EndRendering(commandBuffer);

      // Renderer.BeginRendering(commandBuffer);
      if (UseImGui) {
        GuiController.Update(Time.StopwatchDelta);
      }
      Systems.UpdateSystems2(toUpdate, _currentFrame);
      var updatable = _entities.Where(x => x.CanBeDisposed == false).ToArray();
      MasterRenderUpdate(updatable.GetScriptsAsSpan());
      _onGUI?.Invoke();
      if (UseImGui) {
        GuiController.Render(_currentFrame);
      }
      Renderer.EndRendering(commandBuffer);

      Renderer.EndFrame();

      if (Systems.Render3DSystem != null) {
        StorageCollection.CheckSize("ObjectStorage", frameIndex, Systems.Render3DSystem.LastKnownElemCount, _descriptorSetLayouts["ObjectData"], default);
        StorageCollection.CheckSize("JointsStorage", frameIndex, (int)Systems.Render3DSystem.LastKnownSkinnedElemJointsCount, _descriptorSetLayouts["JointsBuffer"], default);
      }

      while (_reloadQueue.Count > 0) {
        var item = _reloadQueue.Dequeue();
        item.Dispose();
        item.Init(item.AABBFilter);
      }
    }

    Collect();

    if (_entitiesQueue.Count > 0) {
      Mutex.WaitOne();
      while (_entitiesQueue.Count > 0) {
        _entities.Add(_entitiesQueue.Dequeue());
      }
      Mutex.ReleaseMutex();
    }
  }

  internal unsafe void RenderLoader() {
    var commandBuffer = Renderer.BeginFrame();
    if (commandBuffer != VkCommandBuffer.Null) {
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
    Systems?.UpdateCalculationSystems([.. GetEntities()]);
  }

  internal unsafe void LoaderLoop() {
    var pool = Device.CreateCommandPool();
    var threadInfo = new ThreadInfo() {
      CommandPool = pool,
      CommandBuffer = [Renderer.MAX_FRAMES_IN_FLIGHT]
    };

    VkCommandBufferAllocateInfo secondaryCmdBufAllocateInfo = new() {
      level = VkCommandBufferLevel.Primary,
      commandPool = threadInfo.CommandPool,
      commandBufferCount = 1
    };

    fixed (VkCommandBuffer* cmdBfPtr = threadInfo.CommandBuffer) {
      vkAllocateCommandBuffers(Device.LogicalDevice, &secondaryCmdBufAllocateInfo, cmdBfPtr).CheckResult();
    }

    Renderer.CreateCommandBuffers(threadInfo.CommandPool, CommandBufferLevel.Primary);

    while (!_renderShouldClose) {
      RenderLoader();
    }

    fixed (VkCommandBuffer* cmdBfPtrEnd = threadInfo.CommandBuffer) {
      // vkFreeCommandBuffers(
      //   Device.LogicalDevice,
      //   threadInfo.CommandPool,
      //   (uint)Renderer.MAX_FRAMES_IN_FLIGHT,
      //   cmdBfPtrEnd
      // );

      vkFreeCommandBuffers(
        Device.LogicalDevice,
        threadInfo.CommandPool,
        (uint)threadInfo.CommandBuffer.Length,
        cmdBfPtrEnd
      );
    }

    Device.WaitQueue();
    Device.WaitDevice();

    vkDestroyCommandPool(Device.LogicalDevice, threadInfo.CommandPool, null);

    _renderShouldClose = false;
  }

  internal unsafe void RenderLoop() {
    Mutex.WaitOne();
    var pool = Device.CreateCommandPool();
    var threadInfo = new ThreadInfo() {
      CommandPool = pool,
      CommandBuffer = [Renderer.MAX_FRAMES_IN_FLIGHT]
    };

    Renderer.CreateCommandBuffers(threadInfo.CommandPool, CommandBufferLevel.Primary);
    // Renderer.BuildCommandBuffers(() => { });
    Mutex.ReleaseMutex();

    while (!_renderShouldClose) {
      // Logger.Warn("SPINNING " + !_renderShouldClose);
      if (Window.IsMinimalized) continue;

      Render(threadInfo);

      GC.Collect(2, GCCollectionMode.Optimized, false);
    }

    Logger.Info("[RENDER LOOP] Closing Renderer");

    Device.WaitQueue();
    Device.WaitDevice();

    vkDestroyCommandPool(Device.LogicalDevice, threadInfo.CommandPool, null);

    _renderShouldClose = false;
  }
  #endregion APPLICATION_LOOP

  private void Cleanup() {
    _skybox?.Dispose();
    GuiController?.Dispose();

    Span<Entity> entities = _entities.ToArray();
    for (int i = 0; i < entities.Length; i++) {
      // entities[i].GetComponent<Sprite>()?.Dispose();

      // var u = entities[i].GetDrawables<IDrawable>();
      // foreach (var e in u) {
      //   var t = e as IDrawable;
      //   t?.Dispose();
      // }
      entities[i].DisposeEverything();
    }

    _textureManager?.Dispose();
    foreach (var layout in _descriptorSetLayouts) {
      layout.Value.Dispose();
    }
    _globalPool.Dispose();
    unsafe {
      MemoryUtils.FreeIntPtr<GlobalUniformBufferObject>((nint)_ubo);
    }
    StorageCollection?.Dispose();
    Systems?.Dispose();
    Renderer?.Dispose();
    Window?.Dispose();
    if (Allocator != IntPtr.Zero) {
      Logger.Info("[ALLOCATION] Disposing Allocator");
      vmaDestroyAllocator(Allocator);
    }
    Device?.Dispose();
  }

  private void Collect() {
    if (_entities.Count == 0) return;
    for (short i = 0; i < _entities.Count; i++) {
      if (_entities[i].CanBeDisposed) {


        if (_entities[i].Collected) continue;

        _entities[i].Collected = true;
        _entities[i].DisposeEverything();
        RemoveEntity(_entities[i].EntityID);
      }
    }
  }

  public void CloseApp() {
    _appExitRequest = true;
  }

  private async void HandleExit() {
    await Coroutines.CoroutineRunner.Instance.StopAllCoroutines();

    Guizmos.Clear();
    Guizmos.Free();
    foreach (var e in _entities) {
      e.CanBeDisposed = true;
    }

    Logger.Info($"Waiting for entities to dispose... [{_entities.Count()}]");
    if (_entities.Count() > 0) {
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