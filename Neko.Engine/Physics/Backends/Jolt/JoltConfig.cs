namespace Neko.Physics;

public static class JoltConfig {
  public static int MaxBodies { get; private set; } = 65536;
  public static int NumBodyMutexes { get; private set; } = 0;
  public static int MaxBodyPairs { get; private set; } = 65536;
  public static int MaxContactConstraints { get; private set; } = 65536;

  public static float WorldScale = 1.0f;

  internal static class Layers {
    public const byte NonMoving = 0;
    public const byte Moving = 1;
  }

  internal static class BroadPhaseLayers {
    public const byte NonMoving = 0;
    public const byte Moving = 1;
  }
}
