using Dwarf.Rendering;
using Dwarf.Rendering.Renderer2D;
using Dwarf.Rendering.Renderer2D.Interfaces;
using Dwarf.Rendering.Renderer3D;

namespace Dwarf.EntityComponentSystem;

public static class EntityHelper {
  public static Entity[] Distinct<T>(this List<Entity> entities) where T : Component {
    return entities.Where(e => !e.CanBeDisposed && e.HasComponent<T>()).ToArray();
  }
  public static ReadOnlySpan<Entity> DistinctAsReadOnlySpan<T>(this List<Entity> entities) where T : Component {
    return entities.Where(e => !e.CanBeDisposed && e.HasComponent<T>()).ToArray();
  }

  public static Span<Entity> DistinctAsSpan<T>(this List<Entity> entities) where T : Component {
    return entities.Where(e => !e.CanBeDisposed && e.HasComponent<T>()).ToArray();
  }

  public static ReadOnlySpan<Entity> DistinctReadOnlySpan<T>(this ReadOnlySpan<Entity> entities) where T : Component {
    var returnEntities = new List<Entity>();
    for (int i = 0; i < entities.Length; i++) {
      if (!entities[i].CanBeDisposed && entities[i].HasComponent<T>()) returnEntities.Add(entities[i]);
    }
    return returnEntities.ToArray();
  }

  public static ReadOnlySpan<Entity> Distinct<T>(this Entity[] entities) where T : Component {
    var returnEntities = new List<Entity>();
    for (int i = 0; i < entities.Length; i++) {
      if (!entities[i].CanBeDisposed && entities[i].HasComponent<T>()) returnEntities.Add(entities[i]);
    }
    return returnEntities.ToArray();
  }

  public static Span<Entity> DistinctInterface<T>(this List<Entity> entities) where T : IDrawable {
    return entities.Where(e => !e.CanBeDisposed && e.IsDrawable<T>()).ToArray();
  }

  public static Span<Entity> DistinctInterface<T>(this Entity[] entities) where T : IDrawable {
    return entities.Where(e => !e.CanBeDisposed && e.IsDrawable<T>()).ToArray();
  }

  public static Span<IRender3DElement> DistinctI3D(this Entity[] entities) {
    var drawables3D = new List<IRender3DElement>();
    for (int i = 0; i < entities.Length; i++) {
      if (entities[i].CanBeDisposed) continue;
      if (entities[i].GetDrawable<IRender3DElement>() is IRender3DElement target) {
        drawables3D.Add(target);
      }
    }
    return drawables3D.ToArray();
  }

  public static Span<IDrawable2D> DistinctI2D(this Entity[] entities) {
    // int len = entities.Length;
    // var buffer = new IDrawable2D[len];
    var buffer = new List<IDrawable2D>();
    // int count = 0;

    for (int i = 0; i < entities.Length; i++) {
      var e = entities[i];
      if (e.CanBeDisposed)
        continue;

      var drawable = e.GetDrawable<IDrawable2D>();
      if (drawable == null) continue;

      var castDrawable = (IDrawable2D)drawable;

      // if (castDrawable.Children.Length > 0) {
      //   for (int j = 0; j < castDrawable.Children.Length; j++) {
      //     // buffer[count++] = castDrawable.Children[j];
      //     buffer.Add(castDrawable.Children[j]);
      //     count++;
      //   }
      // } else {
      //   // buffer[count++] = (IDrawable2D)drawable;
      //   buffer.Add(castDrawable);
      //   count++;
      // }

      buffer.Add(castDrawable);
      // count++;
    }

    if (buffer.Count != 0) {
      // Array.Sort(buffer, 0, count, Drawable2DComparer.Instance);
      buffer.Sort(Drawable2DComparer.Instance);
    }

    // return new Span<IDrawable2D>(buffer.ToArray(), 0, count);
    return buffer.ToArray();
  }

  public static ReadOnlySpan<Entity> DistinctInterface<T>(this ReadOnlySpan<Entity> entities) where T : IDrawable {
    var returnEntities = new List<Entity>();
    for (int i = 0; i < entities.Length; i++) {
      if (entities[i].CanBeDisposed) continue;
      if (entities[i].IsDrawable<T>()) returnEntities.Add(entities[i]);
    }
    return returnEntities.ToArray();
  }

  public static ReadOnlySpan<DwarfScript> GetScripts(this List<Entity> entities) {
    var list = new List<DwarfScript>();

    foreach (var e in entities.Where(x => !x.CanBeDisposed)) {
      list.AddRange(e.GetScripts());
    }

    return list.ToArray();
  }

  public static ReadOnlySpan<DwarfScript> GetScriptsAsSpan(this Entity[] entities) {
    var list = new List<DwarfScript>();

    foreach (var e in entities.Where(x => !x.CanBeDisposed)) {
      list.AddRange(e.GetScripts());
    }

    return list.ToArray();
  }

  public static DwarfScript[] GetScriptsAsArray(this Entity[] entities) {
    var list = new List<DwarfScript>();

    foreach (var e in entities.Where(x => !x.CanBeDisposed)) {
      list.AddRange(e.GetScripts());
    }

    return [.. list];
  }

  public static ReadOnlySpan<Entity> AsReadOnlySpan(this List<Entity> entities) {
    var tmpList = new List<Entity>();
    for (int i = 0; i < entities.Count; i++) {
      if (entities[i].CanBeDisposed) continue;

      var item = entities.ElementAtOrDefault(i);
      if (item is null) continue;

      tmpList.Add(item);
    }

    return tmpList.ToArray();
  }

  public static Entity[] AsArray(this List<Entity> entities) {
    var tmpList = new List<Entity>();
    for (int i = 0; i < entities.Count; i++) {
      var targetRef = entities[i];

      if (targetRef == null) continue;
      if (targetRef.CanBeDisposed) continue;

      var item = entities.ElementAtOrDefault(i);
      if (item is null) continue;

      tmpList.Add(item);
    }

    return [.. tmpList];
  }

  private sealed class Drawable2DComparer : IComparer<IDrawable2D> {
    public static readonly Drawable2DComparer Instance = new();
    private Drawable2DComparer() { }

    public int Compare(IDrawable2D? a, IDrawable2D? b) {
      if (a != null && a.Entity.CanBeDisposed) return 0;
      if (b != null && b.Entity.CanBeDisposed) return 0;
      float az = a!.Entity.GetComponent<Transform>().Position.Z;
      float bz = b!.Entity.GetComponent<Transform>().Position.Z;
      if (az < bz) return -1;
      if (az > bz) return 1;
      return 0;
    }
  }
}
