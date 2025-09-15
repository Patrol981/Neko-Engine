// using Dwarf.Extensions.GLFW;
using System.Numerics;
using Dwarf.Extensions.Logging;
using Dwarf.Rendering.UI;
using Dwarf.Testing;
using Dwarf.Windowing;
using SDL3;

using static SDL3.SDL3;

namespace Dwarf.Globals;

public class MouseButtons {
  public bool Left { get; set; }
  public bool Right { get; set; }
  public bool Middle { get; set; }
}

public static class Input {
  public class InputState {
    public bool Down { get; set; }
    public bool Pressed { get; set; }
  }


  public static unsafe bool GetKey(Scancode scancode) {
    var state = SDL_GetKeyboardState(null);
    return state[(int)scancode];
  }

  public static unsafe bool GetKeyDown(Keycode scancode) {
    bool keyPressed = s_keyStates[(int)scancode].Pressed;
    s_keyStates[(int)scancode].Pressed = false;

    return keyPressed;
  }

  public static bool GetMouseButtonDown(MouseButton button) {
    bool mouseBtnPressed = false;

    switch (button) {
      case MouseButton.Left:
        mouseBtnPressed = MouseButtons.Left;
        MouseButtons.Left = false;
        return mouseBtnPressed;
      case MouseButton.Middle:
        mouseBtnPressed = MouseButtons.Middle;
        MouseButtons.Middle = false;
        return mouseBtnPressed;
      case MouseButton.Right:
        mouseBtnPressed = MouseButtons.Right;
        MouseButtons.Right = false;
        return mouseBtnPressed;
      default:
        return mouseBtnPressed;
    }
  }

  public static unsafe bool GetMouseButton(MouseButton button) {
    var state = SDL_GetMouseState(null, null);
    return (state & SDL_BUTTON((SDL_Button)button)) != 0;
  }

  public static bool GetGamepadButtonDown(GamepadButtons gamepadButton) {
    if (Window.IsGameControllerNull) return false;

    bool buttonPressed = s_gamepadButtons[(int)gamepadButton].Pressed;
    s_gamepadButtons[(int)gamepadButton].Pressed = false;

    return buttonPressed;
  }

  public static bool GetGamepadButton(GamepadButtons gamepadButton) {
    if (Window.IsGameControllerNull) return false;
    if (SDL_GetGamepadButton(Window.GameController, (SDL_GamepadButton)gamepadButton)) {
      return true;
    }
    return false;
  }

  public static bool MouseOverUI() {
    return ImGuiController.MouseOverUI();
  }

  #region Keyboard

  private static Dictionary<int, InputState> s_keyStates = EnumerateKeys();
  public static Dictionary<int, InputState> KeyStates => s_keyStates;

  private static Dictionary<int, InputState> EnumerateKeys() {
    var keys = new Dictionary<int, InputState>();

    foreach (var enumValue in Enum.GetValuesAsUnderlyingType(typeof(Keycode))) {
      keys.TryAdd((int)(uint)enumValue, new());
    }

    return keys;
  }

  public static void KeyCallback(SDL_Window window, SDL_KeyboardEvent e, SDL_EventType a) {
    switch (a) {
      case SDL_EventType.KeyDown:
        s_keyStates[(int)e.key].Down = true;
        s_keyStates[(int)e.key].Pressed = true;

        if (e.key == SDL_Keycode.F) Application.Instance.Window.FocusOnWindow();
        if (e.key == SDL_Keycode.F1) Application.Instance.Window.MaximizeWindow();
        if (e.key == SDL_Keycode.Grave) PerfMonitor.ChangeWireframeMode();
        if (e.key == SDL_Keycode._1) PerfMonitor.ChangeDebugVisiblity();

        PerformanceTester.KeyHandler(e.key);

        break;
      case SDL_EventType.KeyUp:
        s_keyStates[(int)e.key].Down = false;
        break;
      default:
        break;
    }
  }

  #endregion
  #region Gamepad

  private static Dictionary<int, InputState> s_gamepadButtons = EnumerateGamepadButtons();

  private static Dictionary<int, InputState> EnumerateGamepadButtons() {
    var keys = new Dictionary<int, InputState>();

    foreach (var enumValue in Enum.GetValuesAsUnderlyingType(typeof(GamepadButtons))) {
      keys.TryAdd((int)enumValue, new());
    }

    return keys;
  }

  public static void GamepadCallback(SDL_Window window, SDL_GamepadButtonEvent e, SDL_EventType a) {
    switch (a) {
      case SDL_EventType.GamepadButtonDown:
        s_gamepadButtons[(int)e.Button].Down = true;
        s_gamepadButtons[(int)e.Button].Pressed = true;
        break;
      case SDL_EventType.GamepadButtonUp:
        s_gamepadButtons[(int)e.Button].Down = false;
        break;
    }
  }

  #endregion
  #region Mouse

  public static event EventHandler? ClickEvent;
  public static double ScrollDelta { get; set; } = 0.0;
  public static double PreviousScroll { get; private set; } = 0.0;

  public static MouseButtons MouseButtons { get; set; } = new() {
    Left = false,
    Right = false,
    Middle = false,
  };

  public static MouseButtons QuickStateMouseButtons { get; } = new() {
    Left = false,
    Right = false,
    Middle = false,
  };
  private static Vector2 s_lastMousePositionFromCallback = Vector2.Zero;
  private static Vector2 s_lastRelativeMousePositionFromCallback = Vector2.Zero;

  public static void WindowMouseCallback(float xpos, float ypos) {
    s_lastMousePositionFromCallback.X = xpos;
    s_lastMousePositionFromCallback.Y = ypos;
  }

  public static void RelativeMouseCallback(float xpos, float ypos) {
    s_lastRelativeMousePositionFromCallback.X += xpos;
    s_lastRelativeMousePositionFromCallback.Y += ypos;
  }

  public static unsafe void ScrollCallback(double xoffset, double yoffset) {
    double currentScrollY = yoffset;
    ScrollDelta = currentScrollY += yoffset;
    PreviousScroll = currentScrollY;
  }

  public static void MouseButtonCallbackUp(SDL_Button button) {
    switch (button) {
      case SDL_Button.Left:
        QuickStateMouseButtons.Left = false;
        break;
      case SDL_Button.Middle:
        QuickStateMouseButtons.Middle = false;
        break;
      case SDL_Button.Right:
        QuickStateMouseButtons.Right = false;
        break;
      default:
        break;
    }
  }

  public static void MouseButtonCallbackDown(SDL_Button button) {
    switch (button) {
      case SDL_Button.Left:
        MouseButtons.Left = true;
        QuickStateMouseButtons.Left = true;
        break;
      case SDL_Button.Middle:
        MouseButtons.Middle = true;
        QuickStateMouseButtons.Middle = true;
        break;
      case SDL_Button.Right:
        MouseButtons.Right = true;
        QuickStateMouseButtons.Right = true;
        break;
      default:
        break;
    }
  }

  private static void OnClicked(EventArgs e) {
    ClickEvent?.Invoke(ClickEvent, e);
  }

  public static Vector2 MousePosition {
    get {
      return Window.MouseCursorState switch {
        CursorState.Centered => s_lastRelativeMousePositionFromCallback,
        _ => s_lastMousePositionFromCallback,
      };
    }
    set {
      switch (Window.MouseCursorState) {
        case CursorState.Centered:
          s_lastRelativeMousePositionFromCallback = value;
          break;
        default:
          s_lastMousePositionFromCallback = value;
          break;
      }
    }
  }

  #endregion
}
