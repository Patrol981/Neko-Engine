using Dwarf;
using Dwarf.EntityComponentSystem;
using Dwarf.Rendering.Renderer3D;
using ImGuiNET;

namespace Dwarf.Rendering.UI.Utils;

public partial class EditorUtils {
  private const string Tab = "\t";

  public static void NodeView(Entity? target) {
    if (target == null) return;

    var nodes = target.GetDrawable3D()!.Nodes;

    ImGui.Begin("Node View - " + target.Name);

    foreach (var node in nodes) {
      HandleNode(node, "");
    }

    ImGui.End();
  }

  private static void HandleNode(Node node, string currDepth) {
    ImGui.Text($"{currDepth}[NodeID: {node.Index}] {node.Name} ({node.Scale})");
    // ImGui.BeginChild($"[NodeID: {node.Index}] {node.Name}");
    // ImGui.TreeNode($"[NodeID: {node.Index}] {node.Name}");
    // ImGui.TreePop();
    ImGui.NewLine();
    currDepth += Tab;
    foreach (var child in node.Children) {
      HandleNode(child, currDepth);
    }
  }
}