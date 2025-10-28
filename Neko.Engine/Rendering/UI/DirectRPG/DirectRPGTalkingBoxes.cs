using System.Numerics;
using ImGuiNET;

namespace Neko.Rendering.UI.DirectRPG;

public partial class DirectRPG {
  private const string LOREM = "Sed ut perspiciatis, unde omnis iste natus error sit voluptatem accusantium doloremque laudantium, totam rem aperiam eaque ipsa, quae ab illo inventore veritatis et quasi architecto beatae vitae dicta sunt, explicabo. Nemo enim ipsam voluptatem, quia voluptas sit, aspernatur aut odit aut fugit, sed quia consequuntur magni dolores eos, qui ratione voluptatem sequi nesciunt, neque porro quisquam est, qui dolorem ipsum, quia dolor sit, amet, consectetur, adipisci velit, sed quia non numquam eius modi tempora incidunt, ut labore et dolore magnam aliquam quaerat voluptatem. Ut enim ad minima veniam, quis nostrum exercitationem ullam corporis suscipit laboriosam, nisi ut aliquid ex ea commodi consequatur? Quis autem vel eum iure reprehenderit, qui in ea voluptate velit esse, quam nihil molestiae consequatur, vel illum, qui dolorem eum fugiat, quo voluptas nulla pariatur?";

  private const ImGuiWindowFlags SIMPLE_DIALOG_FLAGS = ImGuiWindowFlags.NoTitleBar |
                                                       ImGuiWindowFlags.NoResize |
                                                       ImGuiWindowFlags.NoMove |
                                                       ImGuiWindowFlags.NoCollapse;

  private static Vector2 SIMPLE_DIALOG_SIZE = new(700, 200);

  public static void BeginSimpleDialog(Anchor anchor = Anchor.Bottom) {
    ImGui.SetNextWindowSize(SIMPLE_DIALOG_SIZE);
    SetWindowAlignment(SIMPLE_DIALOG_SIZE, anchor, false);
    ImGui.Begin("Simple Dialog", SIMPLE_DIALOG_FLAGS);

    ImGui.PushFont(GuiController.LargeFont);
    ImGui.Text("Title");
    ImGui.PopFont();

    ImGui.TextWrapped(LOREM);
    EndSimpleDialog();
  }

  private static void EndSimpleDialog() {
    ImGui.End();
  }

  public static void DrawDialogBubble(Vector2 anchorPos, string text) {
    var drawList = ImGui.GetBackgroundDrawList();
    var radius = 50;

    float bubbleWidth = 150f;
    float bubbleHeight = 60f;
    float rounding = 10f;
    uint bubbleColor = 0xFFFFFFFF;
    uint outlineColor = 0xFF000000; // ARGB black

    var textSize = ImGui.CalcTextSize(text);

    // if (anchorPos.X < 0 || anchorPos.X > screen.X ||
    // anchorPos.Y < 0 || anchorPos.Y > screen.Y) {
    //   return;
    // }

    Vector2[] tail = [
      new Vector2(anchorPos.X - 10.0f, anchorPos.Y + radius),
      new Vector2(anchorPos.X + 10.0f, anchorPos.Y + radius),
      new Vector2(anchorPos.X, anchorPos.Y + radius + 20.0f),
    ];
    drawList.AddConvexPolyFilled(ref tail[0], 3, bubbleColor);
    drawList.AddPolyline(ref tail[0], 3, outlineColor, ImDrawFlags.Closed, 2.0f);

    Vector2 rectMin = new Vector2(anchorPos.X - (bubbleWidth * 0.5f), anchorPos.Y);
    Vector2 rectMax = new Vector2(anchorPos.X + (bubbleWidth * 0.5f), anchorPos.Y + bubbleHeight);
    drawList.AddRectFilled(rectMin, rectMax, bubbleColor, rounding, ImDrawFlags.None);
    drawList.AddRect(rectMin, rectMax, outlineColor, rounding, ImDrawFlags.None, 2.0f);

    var textPos = new Vector2(
        anchorPos.X - (textSize.X * 0.5f),
        anchorPos.Y + (bubbleHeight * 0.5f) - (textSize.Y * 0.5f)
    );
    drawList.AddText(textPos, 0xFF000000, text);
  }

  public static void BeginDialogWith3DModel() {
    throw new NotImplementedException();
    // EndDialogWith3DModel();
  }

  private static void EndDialogWith3DModel() {
    ImGui.End();
  }
}