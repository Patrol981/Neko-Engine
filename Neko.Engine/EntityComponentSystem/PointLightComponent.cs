using System.Numerics;

namespace Neko.EntityComponentSystem;

public class PointLightComponent {
  public Vector4 Color { get; set; }

  public Entity Owner { get; init; }

  public PointLightComponent(Entity owner) {
    Owner = owner;
  }
}
