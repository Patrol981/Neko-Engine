using Neko.Hammer.Enums;
using Neko.Hammer.Structs;

namespace Neko.Hammer.Models;

public class ShapeSettings {
  public Mesh Mesh { get; init; }
  public object? UserData { get; set; }
  public ObjectType ObjectType { get; set; }

  public ShapeSettings(Mesh mesh, object userData, ObjectType objectType) {
    Mesh = mesh;
    UserData = userData;
    ObjectType = objectType;
  }
}