namespace Neko.Physics.Backends;

public enum BackendKind {
  Jolt,
  Hammer,
  Box2D,
  Default = Hammer,
}