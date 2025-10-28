using System.Numerics;
using Neko.EntityComponentSystem;

namespace Neko.Rendering.Guizmos;

public class Guizmo {
  public Guid Id { get; init; }
  public GuizmoType GuizmoType { get; init; }
  public TransformComponent Transform { get; init; }
  public Vector3 Color;

  public Guizmo(GuizmoType guizmoType) {
    GuizmoType = guizmoType;
    Id = Guid.NewGuid();
    Transform = new TransformComponent();
    Color = Vector3.Zero;
  }

  public Guizmo(GuizmoType guizmoType, Vector3 position) {
    GuizmoType = guizmoType;
    Id = Guid.NewGuid();
    Transform = new TransformComponent(position);
    Color = Vector3.Zero;
  }

  public Guizmo(GuizmoType guizmoType, Vector3 position, Vector3 scale) {
    GuizmoType = guizmoType;
    Id = Guid.NewGuid();
    Transform = new TransformComponent(position, Vector3.Zero, scale);
    Color = Vector3.Zero;
  }

  public Guizmo(GuizmoType guizmoType, Vector3 position, Vector3 scale, Vector3 color) {
    GuizmoType = guizmoType;
    Id = Guid.NewGuid();
    Transform = new TransformComponent(position, Vector3.Zero, scale);
    Color = color;
  }
}
