namespace Dwarf.EntityComponentSystemRewrite;

public class Entity {
  public string Name;
  public Guid Id;
  public Dictionary<Type, Guid> Components;

  public bool Active { get; set; }
  public bool CanBeDisposed { get; set; }

  public Entity(string name) {
    Name = name;
    Id = Guid.NewGuid();
    Components = [];
    CanBeDisposed = false;
    Active = true;
  }
}