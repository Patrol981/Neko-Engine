using System.Runtime.InteropServices;
using Neko.Extensions.Logging;
using Neko.Globals;
using Neko.Math;
using Neko.Rendering.UI;
using Neko.Utils;
using SDL3;
using StbImageSharp;
using Vortice.Vulkan;

using static SDL3.SDL3;

namespace Neko.Windowing;

public class Window : IWindow {
  public NekoExtent2D Extent { get; set; }
  public bool ShouldClose { get; set; } = false;
  public bool FramebufferResized { get; set; } = false;
  public bool IsMinimalized { get; private set; } = false;
  public event EventHandler? OnResizedEventDispatcher;
  public float RefreshRate { get; private set; }
  public static SDL_Gamepad GameController { get; private set; }
  protected SDL_Window SDLWindow { get; private set; }
  public static CursorState MouseCursorState = CursorState.Normal;

  private readonly bool _windowMinimalized = false;
  private SDL_Cursor _cursor;

  private Application? _app;

  public void Init(string windowName, bool fullscreen, int width, int height, bool debug = false) {
    InitWindow(windowName, fullscreen, debug, width, height);
    LoadIcons();
    Show();
    RefreshRate = GetRefreshRate();
    Logger.Info($"[WINDOW] Refresh rate set to {RefreshRate}");
    // EnumerateAvailableGameControllers();
  }

  private unsafe void InitWindow(string windowName, bool fullscreen, bool debug, int width, int height) {
    if (!SDL_Init(SDL_InitFlags.Video | SDL_InitFlags.Gamepad | SDL_InitFlags.Audio)) {
      throw new Exception("Failed to initalize Window");
    }

    if (debug) {
      Logger.Info("Setting Debug For SDL");
      SDL_SetLogPriorities(SDL_LogPriority.Verbose);
      SDL_SetLogOutputFunction(LogSDL);
    }

    var windowFlags =
                      // SDL_WindowFlags.Maximized |
                      // SDL_WindowFlags.Transparent |
                      // SDL_WindowFlags.MouseCapture |
                      // SDL_WindowFlags.MouseGrabbed |
                      SDL_WindowFlags.Occluded |
                      SDL_WindowFlags.MouseFocus |
                      SDL_WindowFlags.InputFocus |
                      SDL_WindowFlags.HighPixelDensity |
                      SDL_WindowFlags.Resizable;

    if (
      RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
      RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
    ) {
      if (!SDL_Vulkan_LoadLibrary()) {
        throw new Exception("Failed to initialize Vulkan");
      }

      windowFlags |= SDL_WindowFlags.Vulkan;
    } else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
      var path = Path.Combine(NekoPath.AssemblyDirectory, "libMoltenVK.dylib");

      Logger.Info($"[WINDOW] Setting path for MoltenVK - {path}");

      if (!SDL_Vulkan_LoadLibrary(path)) {
        throw new Exception("Failed to initialize Vulkan");
      }

      windowFlags |= SDL_WindowFlags.Vulkan;
    }
    // } else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
    //   windowFlags |= SDL_WindowFlags.Metal;
    // }

    if (fullscreen && !RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
      windowFlags |= SDL_WindowFlags.Fullscreen | SDL_WindowFlags.Borderless;
    } else if (fullscreen && RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
      windowFlags |= SDL_WindowFlags.Fullscreen;
    }

    SDLWindow = SDL_CreateWindow(windowName, width, height, windowFlags);
    if (SDLWindow.IsNull) {
      throw new Exception("Failed to create SDL window");
    }

    _ = SDL_SetWindowPosition(SDLWindow, SDL_WINDOWPOS_CENTERED, SDL_WINDOWPOS_CENTERED);
    // SDL_SetWindowOpacity(SDLWindow, 0.1f);

    _app = Application.Instance;
    _app.Window.Extent = new NekoExtent2D((uint)width, (uint)height);
  }

  private unsafe void LoadIcons() {

    // We bundle MacOS variant into .app so we can skip setting icon since it will be set
    // from appbundle
    if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
      var engineIcoStream = File.OpenRead($"{NekoPath.AssemblyDirectory}/Resources/ico/neko_ico.png");
      var engineIco = ImageResult.FromStream(engineIcoStream, ColorComponents.RedGreenBlueAlpha);
      var engineSurface = SDL_CreateSurface(engineIco.Width, engineIco.Height, SDL_PixelFormat.Abgr8888);
      fixed (byte* pixPtr = engineIco.Data) {
        engineSurface->pixels = (nint)pixPtr;
      }

      if (!SDL_SetWindowIcon(SDLWindow, engineSurface)) {
        Logger.Error("Failed to load window icon");
      }
      Marshal.FreeHGlobal((nint)engineSurface);
      engineIcoStream.Dispose();
    }

    var cursorIcoStream = File.OpenRead($"{NekoPath.AssemblyDirectory}/Resources/ico/cursor.png");
    var cursorIco = ImageResult.FromStream(cursorIcoStream, ColorComponents.RedGreenBlueAlpha);
    var cursorSurface = SDL_CreateSurface(cursorIco.Width, cursorIco.Height, SDL_PixelFormat.Argb8888);
    fixed (byte* cursorPtr = cursorIco.Data) {
      cursorSurface->pixels = (nint)cursorPtr;
    }
    _cursor = SDL_CreateColorCursor(cursorSurface, 0, 0);
    SDL_SetCursor(_cursor);
    cursorIcoStream.Dispose();
  }

  public void Show() {
    _ = SDL_ShowWindow(SDLWindow);
  }

  public void Dispose() {
    SDL_DestroyWindow(SDLWindow);
  }

  public void ResetWindowResizedFlag() {
    FramebufferResized = false;
  }

  public void PollEvents() {
    while (SDL_PollEvent(out SDL_Event e)) {
      switch (e.type) {
        case SDL_EventType.Quit:
          ShouldClose = true;
          break;
        case SDL_EventType.KeyDown:
          Input.KeyCallback(SDLWindow, e.key, e.type);
          break;
        case SDL_EventType.GamepadButtonDown:
          Input.GamepadCallback(SDLWindow, e.gbutton, e.type);
          break;
        case SDL_EventType.MouseMotion:
          switch (MouseCursorState) {
            case CursorState.Centered:
              Input.RelativeMouseCallback(e.motion.xrel, e.motion.yrel);
              break;
            case CursorState.Normal:
              Input.WindowMouseCallback(e.motion.x, e.motion.y);
              break;
            default:
              break;
          }
          break;
        case SDL_EventType.MouseWheel:
          Input.ScrollCallback(e.wheel.x, e.wheel.y);
          break;
        case SDL_EventType.MouseButtonUp:
          Input.MouseButtonCallbackUp(e.button.Button);
          break;
        case SDL_EventType.MouseButtonDown:
          Input.MouseButtonCallbackDown(e.button.Button);
          break;
        case SDL_EventType.WindowResized:
          FrambufferResizedCallback(e.window.data1, e.window.data2);
          break;
        case SDL_EventType.WindowMaximized:
          FrambufferResizedCallback(e.window.data1, e.window.data2);
          break;
        case SDL_EventType.WindowRestored:
          IsMinimalized = false;
          // FrambufferResizedCallback(e.window.data1, e.window.data2);
          break;
        case SDL_EventType.WindowMinimized:
          IsMinimalized = true;
          break;
        case SDL_EventType.LowMemory:
          throw new Exception("Memory Leak");
        case SDL_EventType.GamepadAdded:
          if (GameController.IsNull) {
            GameController = SDL_OpenGamepad(e.gdevice.which);
            Logger.Info($"[SDL] Connected {SDL_GetGamepadName(GameController)}");
          }
          break;
        case SDL_EventType.GamepadRemoved:
          var instanceId = e.gdevice.which;
          if (GameController.IsNotNull && SDL_GetGamepadID(GameController) == instanceId) {
            Logger.Info($"[SDL] Disconnected {SDL_GetGamepadName(GameController)}");
            SDL_CloseGamepad(GameController);
          }
          break;
        default:
          break;
      }

      if (_app != null && _app.UseImGui) {
        ImGuiController.ImGuiSdl3ProcessEvent(e);
      }
    }
  }

  public void WaitEvents() { }

  private static unsafe void FrambufferResizedCallback(int width, int height) {
    if (width <= 0 || height <= 0) return;
    Logger.Info($"RESISING {width} {height}");
    Application.Instance.Window.FramebufferResized = true;
    Application.Instance.Window.Extent = new NekoExtent2D((uint)width, (uint)height);
    Application.Instance.Window.OnResizedEvent(null!);
  }

  private static void LogSDL(SDL_LogCategory category, SDL_LogPriority priority, string? description) {
    if (priority >= SDL_LogPriority.Error) {
      Logger.Error($"[{priority}] SDL: {description}");
      throw new Exception(description);
    } else {
      Logger.Info($"[{priority}] SDL: {description}");
    }
  }

  public void OnResizedEvent(EventArgs e) {
    OnResizedEventDispatcher?.Invoke(this, e);
  }

  public bool WasWindowResized() => FramebufferResized;
  public bool WasWindowMinimalized() => _windowMinimalized;

  public unsafe ulong CreateSurface(nint instance) {
    switch (Application.Instance.CurrentAPI) {
      case AbstractionLayer.RenderAPI.Vulkan:
        VkSurfaceKHR surface;
        return SDL_Vulkan_CreateSurface(SDLWindow, instance, IntPtr.Zero, (ulong**)&surface) == false
          ? throw new Exception("Failed to create SDL Surface")
          : surface;
      default:
        throw new NotImplementedException();
    }
  }

  public unsafe float GetRefreshRate() {
    var displays = SDL_GetDisplays();
    var displayMode = SDL_GetCurrentDisplayMode(displays[0]);
    return displayMode->refresh_rate;
  }

  public unsafe void SetCursorMode(CursorState cursorState) {
    var prevMousePos = Input.MousePosition;

    MouseCursorState = cursorState;

    Logger.Info($"Setting cursor state to: {MouseCursorState}");

    switch (cursorState) {
      case CursorState.Normal:
        SDL_SetWindowRelativeMouseMode(SDLWindow, false);
        break;
      case CursorState.Centered:
        SDL_SetWindowRelativeMouseMode(SDLWindow, true);
        Input.MousePosition = prevMousePos;
        // SDL_WarpMouseInWindow(s_Window.SDLWindow, s_Window.Size.X / 2, s_Window.Size.Y / 2);
        break;
      case CursorState.Hidden:
        SDL_SetWindowRelativeMouseMode(SDLWindow, false);
        SDL_HideCursor();
        break;
    }
  }

  public void FocusOnWindow() {
    if (MouseCursorState == CursorState.Centered) {
      SetCursorMode(CursorState.Normal);
    } else {
      SetCursorMode(CursorState.Centered);
    }
  }

  public unsafe void MaximizeWindow() {
    Application.Instance.Device.WaitDevice();
    SDL_MaximizeWindow(SDLWindow);
  }

  public void EnumerateAvailableGameControllers() {
    var gamepads = SDL_GetJoysticks();
    foreach (var gamepad in gamepads) {
      if (SDL_IsGamepad(gamepad)) {
        GameController = SDL_OpenGamepad(gamepad);
        Logger.Info($"[SDL] Connected {SDL_GetGamepadName(GameController)}");
      }
    }
  }

  public static bool IsGameControllerNull => GameController.IsNull;
}