using System.Collections.Concurrent;
using Dwarf.EntityComponentSystem;
using Dwarf.Physics;
using Dwarf.Rendering.Renderer2D.Components;
using Dwarf.Rendering.Renderer2D.Interfaces;

namespace Dwarf;

public partial class Application {
  public HashSet<EntityComponentSystemRewrite.Entity> NewEntities = [];
  public ConcurrentDictionary<Guid, DwarfScript> Scripts = [];
  public ConcurrentDictionary<Guid, EntityComponentSystemRewrite.TransformComponent> TransformComponents = [];
  public ConcurrentDictionary<Guid, IDrawable2D> Sprites = [];
  public ConcurrentDictionary<Guid, Rigidbody2D> Rigidbodies2D = [];
}