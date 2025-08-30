using System.Numerics;

using Dwarf.EntityComponentSystem;
using Dwarf.Rendering.Renderer2D;

namespace Dwarf.Math;

public class Collision2D {
  public static bool MouseClickedCollision(I2DCollision coll, Camera camera, Vector2 screenSize) {
    var mouseRay = Ray.MouseToWorld2D(camera, screenSize);

    var bounds = coll.Bounds;

    var collides =
      mouseRay.X >= bounds.Min.X && mouseRay.X <= bounds.Max.X &&
      mouseRay.Y >= bounds.Min.Y && mouseRay.Y <= bounds.Max.Y;

    return collides;
  }

  // public static bool CheckCollisionAABB(I2DCollision a, I2DCollision b) {
  //   var compA = a as Component;
  //   var compB = b as Component;

  //   if (a.IsUI && !b.IsUI) throw new Exception("Cannot compare UI and non UI element");

  //   if (a.IsUI) {
  //     var aTransform = compA!.Owner!.GetComponent<RectTransform>();
  //     var bTransform = compB!.Owner!.GetComponent<RectTransform>();

  //     return aTransform.Position.X < bTransform.Position.X + b.Size.X &&
  //     aTransform.Position.X + a.Size.X > bTransform.Position.X &&
  //     aTransform.Position.Y < bTransform.Position.Y + b.Size.Y &&
  //     a.Size.Y + aTransform.Position.Y > bTransform.Position.Y;
  //   } else {
  //     var aTransform = compA!.Owner!.GetComponent<Transform>();
  //     var bTransform = compB!.Owner!.GetComponent<Transform>();

  //     return aTransform.Position.X < bTransform.Position.X + b.Size.X &&
  //     aTransform.Position.X + a.Size.X > bTransform.Position.X &&
  //     aTransform.Position.Y < bTransform.Position.Y + b.Size.Y &&
  //     a.Size.Y + aTransform.Position.Y > bTransform.Position.Y;
  //   }
  // }

  // public static bool CheckCollisionAABB(Sprite a, Sprite b) {
  //   var aTransform = a.Owner!.GetComponent<Transform>();
  //   var bTransform = b.Owner!.GetComponent<Transform>();

  //   return aTransform.Position.X < bTransform.Position.X + b.Size.X &&
  //     aTransform.Position.X + a.Size.X > bTransform.Position.X &&
  //     aTransform.Position.Y < bTransform.Position.Y + b.Size.Y &&
  //     a.Size.Y + aTransform.Position.Y > bTransform.Position.Y;
  // }

  // public static ReadOnlySpan<Sprite> CollidesWithAABB(Entity[] colls2D, Entity target) {
  //   List<Sprite> colliders = new List<Sprite>();
  //   ReadOnlySpan<Entity> withoutTarget = colls2D
  //                                         .Where(x => x.EntityID != target.EntityID)
  //                                         .ToArray();
  //   var targetSprite = target.GetComponent<Sprite>();
  //   for (int i = 0; i < withoutTarget.Length; i++) {
  //     var iSprite = withoutTarget[i].GetComponent<Sprite>();
  //     if (CheckCollisionAABB(targetSprite, iSprite)) {
  //       colliders.Add(iSprite);
  //     }
  //   }

  //   return colliders.ToArray();
  // }
}
