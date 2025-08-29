using System.Numerics;

using Dwarf.Rendering.UI;

namespace Dwarf.EntityComponentSystemLegacy;

public class RectTransform : Transform {
  public Anchor Anchor { get; set; }
  public Vector2 OffsetFromVector { get; set; }
  public float OriginScale { get; set; } = 1.0f;
  internal uint LastScreenX { get; set; } = 0;
  internal uint LastScreenY { get; set; } = 0;
  internal float LastGlobalScale { get; set; } = 0.0f;
  internal bool RequireUpdate { get; set; } = true;

  public RectTransform() : base() { }

  public RectTransform(Vector3 position) : base(position) { }

  public void SetRequireState() {
    RequireUpdate = true;
  }
}
