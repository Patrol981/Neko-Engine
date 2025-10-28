using System.Numerics;
using Neko.Globals;
using Neko.Windowing;

namespace Neko.Math;

public static class Converter {
  public static float DegreesToRadians(float deg) {
    float rad = MathF.PI / 180 * deg;
    return rad;
  }

  public static float RadiansToDegrees(float rad) {
    float deg = 180 / MathF.PI * rad;
    return deg;
  }

  public static Vector2I ToVec2I(this Vector2 vec2) {
    return new Vector2I((int)vec2.X, (int)vec2.Y);
  }

  public static Vector2 FromVec2I(this Vector2I vec2) {
    return new Vector2(vec2.X, vec2.Y);
  }

  public static Vector4I ToVec4I(this Vector4 vec4) {
    return new Vector4I((int)vec4.X, (int)vec4.Y, (int)vec4.Z, (int)vec4.W);
  }

  public static Vector2 ToVector2(this Vector3 vec3) {
    return new Vector2(vec3.X, vec3.Y);
  }
}