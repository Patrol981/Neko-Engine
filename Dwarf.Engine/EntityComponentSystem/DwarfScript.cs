namespace Dwarf.EntityComponentSystem;

public class DwarfScript : ICloneable, IDisposable {
  protected bool DidAwake { get; private set; }
  protected bool DidStart { get; private set; }

  public Entity OwnerNew { get; internal set; } = default!;

  public virtual void Start() {
    if (DidStart) return;
    DidStart = true;
  }
  public virtual void Awake() {
    if (DidAwake) return;
    DidAwake = true;
  }

  /// <summary>
  /// Performs update calculations in <b> Parallel </b>
  /// </summary>
  public virtual void Update() { }

  /// <summary>
  /// Performs update calculations on <b> Main Thread </b>
  /// </summary>
  public virtual void FixedUpdate() { }

  /// <summary>
  /// Invokes update call on <b> Render Thread </b>
  /// </summary>
  public virtual void RenderUpdate() { }

  public virtual void CollisionEnter(Entity? entity, bool IsTrigger) { }

  public virtual void CollisionStay(Entity? entity, bool isTrigger) { }

  public virtual void CollisionExit(Entity? entity, bool isTrigger) { }

  public virtual void CollisionEnter(Entity? entity) { }

  public virtual void CollisionStay(Entity? entity) { }

  public virtual void CollisionExit(Entity? entity) { }

  public virtual object Clone() {
    return MemberwiseClone();
  }

  public virtual void Dispose() {
    GC.SuppressFinalize(this);
  }
}
