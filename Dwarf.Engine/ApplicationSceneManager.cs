using Dwarf.Extensions.Logging;
using Dwarf.Globals;

namespace Dwarf;

public partial class Application {
  public void SetCurrentScene(Scene scene) {
    CurrentScene = scene;
  }
  public void LoadScene(Scene scene) {
    Mutex.WaitOne();
    SetCurrentScene(scene);
    _newSceneShouldLoad = true;
    Mutex.ReleaseMutex();
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
    // _textureManager.Dispose();
    _textureManager.DisposeLocal();
    // _textureManager = new(Allocator, Device);

    StorageCollection = ApplicationFactory.CreateStorageCollection(Allocator, Device);
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

    await LoadEntities();

    Logger.Info($"Loaded entities: {_entities?.Count}");
    Logger.Info($"Loaded textures: {_textureManager?.PerSceneLoadedTextures?.Count}");

    return Task.CompletedTask;
  }
}