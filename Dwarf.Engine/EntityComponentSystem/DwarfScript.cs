namespace Dwarf.EntityComponentSystem;

public class DwarfScript : Component, ICloneable {
  protected bool DidAwake { get; private set; }
  protected bool DidStart { get; private set; }

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
  /// Performs update calculations on <b> Main Threead </b>
  /// </summary>
  public virtual void FixedUpdate() { }
  public virtual void RenderUpdate() { }

  public virtual void CollisionEnter(Entity? entity, bool IsTrigger) { }

  public virtual void CollisionStay(Entity? entity, bool isTrigger) { }

  public virtual void CollisionExit(Entity? entity, bool isTrigger) { }

  public virtual void CollisionEnter(Entity? entity) { }

  public virtual void CollisionStay(Entity? entity) { }

  public virtual void CollisionExit(Entity? entity) { }

  public object Clone() {
    return MemberwiseClone();
  }
}
