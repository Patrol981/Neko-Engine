namespace Neko.Rendering.Renderer3D;

public class Indirect3DBatch {
  public List<KeyValuePair<Node, ObjectData>> NodeObjects = [];
  public string? Name { get; set; }
  public uint Count { get; set; }
}