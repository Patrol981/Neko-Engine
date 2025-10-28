namespace Neko.Math;

public static class SmoothStep {
  public static float Calculate(float edge0, float edge1, float x) {
    x = Clamp.ClampTo((x - edge0) / (edge1 - edge0));

    return x * x * (3.0f - 2.0f * x);
  }
}