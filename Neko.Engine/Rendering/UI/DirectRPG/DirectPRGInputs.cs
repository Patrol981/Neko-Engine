using Neko.Extensions.Logging;
using ImGuiNET;

namespace Neko.Rendering.UI.DirectRPG;

public partial class DirectRPG {
  private static string s_inputBuffer = "";

  public static void TextInput() {
    if (ImGui.InputText("Input", ref s_inputBuffer, 50)) {
      Logger.Info("a");
    }
  }
}