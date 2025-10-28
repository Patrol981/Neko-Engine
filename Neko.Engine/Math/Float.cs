namespace Neko.Math;

public static class Float {
  public static float Lerp(float from, float to, float value) {
    return from + (to - from) * value;
  }

  public static float InverseLerp(float from, float to, float value) {
    if (from == to) {
      return 0.0f;
    }
    return (value - from) / (to - from);
  }
}