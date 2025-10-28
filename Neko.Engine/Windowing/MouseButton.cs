using SDL3;

namespace Neko.Windowing;

public enum MouseButton : uint {
  Left = SDL_Button.Left,
  Middle = SDL_Button.Middle,
  Right = SDL_Button.Right,
  X1 = SDL_Button.X1,
  X2 = SDL_Button.X2,
}