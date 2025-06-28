using Dwarf.Extensions.Logging;
using ImGuiNET;
using SDL3;

namespace Dwarf.Rendering.UI;

public unsafe struct ImGuiSdl3Data {
  SDL_Window* Window;
  UInt64 Time;
  bool* MousePressed;
  SDL_Cursor* MouseCursors;
  char* ClipboardTextData;
  bool MouseCanUseGlobalState;

  public ImGuiSdl3Data() {
  }
};

public partial class ImGuiController {
  private static bool s_shiftModHold = false;

  public unsafe static ImGuiSdl3Data* GetSdl3BackendData() {
    return ImGui.GetCurrentContext() != IntPtr.Zero ? (ImGuiSdl3Data*)ImGui.GetIO().BackendPlatformUserData : null;
  }

  public unsafe static bool ImGuiSdl3ProcessEvent(SDL_Event sdlEvent) {
    var io = ImGui.GetIO();

    switch (sdlEvent.type) {
      case SDL_EventType.TextInput:
        Logger.Info($"TEXT INPUT: {sdlEvent.text.GetText()}");
        io.AddInputCharactersUTF8(sdlEvent.text.GetText());
        return true;
      case SDL_EventType.KeyUp:
        TryMapKey((Windowing.Scancode)sdlEvent.key.scancode, out var upKey);
        if (upKey != ImGuiKey.None) {
          io.AddKeyEvent(upKey, false);
          if (upKey == ImGuiKey.ModShift) {
            s_shiftModHold = false;
          }
        }
        return true;
      case SDL_EventType.KeyDown:
        TryMapKey((Windowing.Scancode)sdlEvent.key.scancode, out var downKey);
        if (downKey != ImGuiKey.None) {
          if (downKey == ImGuiKey.ModShift) {
            s_shiftModHold = true;
          }
          io.AddKeyEvent(downKey, true);
          if (downKey >= ImGuiKey.A && downKey <= ImGuiKey.Z) {
            io.AddInputCharactersUTF8(s_shiftModHold == true ? downKey.ToString() : downKey.ToString().ToLower());
          } else if (downKey == ImGuiKey.Space) {
            io.AddInputCharactersUTF8(" ");
          }
        }
        return true;
      case SDL_EventType.WindowFocusLost:
        io.AddFocusEvent(false);
        return true;
      case SDL_EventType.WindowFocusGained:
        io.AddFocusEvent(true);
        return true;
      default:
        break;
    }

    return false;
  }
}