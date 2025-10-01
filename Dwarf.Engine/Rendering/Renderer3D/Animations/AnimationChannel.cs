namespace Dwarf.Rendering.Renderer3D.Animations;

public enum PathType {
  Translation,
  Rotation,
  Scale
}

public record AnimationChannel {
  public PathType Path;
  public Node Node = null!;
  public int SamplerIndex;

  // public object Clone() {
  //   return new AnimationChannel {
  //     Path = Path,
  //     // Node = Node,
  //     SamplerIndex = SamplerIndex
  //   };
  // }
}