namespace Neko.Hammer.Models;

public class BodyId {
  public Guid Id { get; private init; }

  public BodyId() {
    Id = Guid.NewGuid();
  }
}