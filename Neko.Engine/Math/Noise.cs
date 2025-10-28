using System.Numerics;

namespace Neko.Math;

public static class Noise {
  private static readonly Random s_random = new();

  public static float Interpolate(float a0, float a1, float w) {
    return (a1 - a0) * w + a0;
  }

  public static Vector2 RandomGradientBit(int ix, int iy) {
    // No precomputed gradients mean this works for any number of grid coordinates
    const int w = 8 * sizeof(uint);
    const int s = w / 2; // rotation width
    uint a = (uint)ix, b = (uint)iy;
    a *= 3284157443; b ^= a << s | a >> w - s;
    b *= 1911520717; a ^= b << s | b >> w - s;
    a *= 2048419325;
    double random = a * (3.14159265 / ~(~0u >> 1)); // in [0, 2*Pi]
    Vector2 v = new();
    v.X = MathF.Cos((float)random); v.X = MathF.Sin((float)random);
    return v;
  }

  public static (float x, float y) RandomGradient() {
    float angle = s_random.NextSingle() * 2 * MathF.PI; // Random angle in radians
    float x = (float)MathF.Cos(angle);
    float y = (float)MathF.Sin(angle);
    return (x, y);
  }

  public static float DotGridGradient(int ix, int iy, float x, float y) {
    // Get gradient from integer coordinates
    var gradient = RandomGradient();

    // Compute the distance vector
    float dx = x - (float)ix;
    float dy = y - (float)iy;

    // Compute the dot-product
    return dx * gradient.x + dy * gradient.y;
  }

  public static float Perlin(float x, float y) {
    int x0 = (int)MathF.Floor(x);
    int x1 = x0 + 1;
    int y0 = (int)MathF.Floor(y);
    int y1 = y0 + 1;

    float sx = x - x0;
    float sy = y - y0;

    float n0, n1, ix0, ix1, value;

    n0 = DotGridGradient(x0, y0, x, y);
    n1 = DotGridGradient(x1, y0, x, y);
    ix0 = Interpolate(n0, n1, sx);

    n0 = DotGridGradient(x0, y1, x, y);
    n1 = DotGridGradient(x1, y1, x, y);
    ix1 = Interpolate(n0, n1, sx);

    value = Interpolate(ix0, ix1, sy);
    return value;
  }
}
