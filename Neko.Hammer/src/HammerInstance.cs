using Neko.Hammer.Models;

namespace Neko.Hammer;

public class HammerInstance {
  public HammerInterface HammerInterface { get; set; }
  public HammerWorld HammerWorld { get; set; }

  public delegate void ContactHandler(in BodyId body1, in BodyId body2);
  public delegate void EmptyContactHandler(in BodyId body1);

  public ContactHandler? OnContactAdded;
  public ContactHandler? OnContactPersisted;
  public ContactHandler? OnContactExit;
  public EmptyContactHandler? OnTilemapContactPersised;

  public HammerInstance() {
    HammerWorld = new HammerWorld(this);
    HammerInterface = new HammerInterface(HammerWorld);
  }
}
