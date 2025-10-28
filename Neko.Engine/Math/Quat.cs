using System.Numerics;

namespace Neko.Math;

public static class Quat {
  public static Quaternion FromEuler(Vector3 euler) {
    euler *= 0.5f * (float)MathF.PI / 180f; // Convert degrees to radians and scale by 0.5

    float yaw = euler.Y;
    float pitch = euler.X;
    float roll = euler.Z;

    float cy = (float)MathF.Cos(yaw * 0.5f);
    float sy = (float)MathF.Sin(yaw * 0.5f);
    float cp = (float)MathF.Cos(pitch * 0.5f);
    float sp = (float)MathF.Sin(pitch * 0.5f);
    float cr = (float)MathF.Cos(roll * 0.5f);
    float sr = (float)MathF.Sin(roll * 0.5f);

    Quaternion q = new Quaternion();

    q.W = cr * cp * cy + sr * sp * sy;
    q.X = sr * cp * cy - cr * sp * sy;
    q.Y = cr * sp * cy + sr * cp * sy;
    q.Z = cr * cp * sy - sr * sp * cy;

    return q;
  }

  public static Vector3 ToEuler(Quaternion quaternion) {
    float yaw;
    float pitch;
    float roll;

    // Roll (x-axis rotation)
    float sinr_cosp = 2 * (quaternion.W * quaternion.X + quaternion.Y * quaternion.Z);
    float cosr_cosp = 1 - 2 * (quaternion.X * quaternion.X + quaternion.Y * quaternion.Y);
    roll = (float)System.Math.Atan2(sinr_cosp, cosr_cosp);

    // Pitch (y-axis rotation)
    float sinp = 2 * (quaternion.W * quaternion.Y - quaternion.Z * quaternion.X);
    if (System.Math.Abs(sinp) >= 1)
      pitch = (float)System.Math.CopySign(System.Math.PI / 2, sinp); // use 90 degrees if out of range
    else
      pitch = (float)System.Math.Asin(sinp);

    // Yaw (z-axis rotation)
    float siny_cosp = 2 * (quaternion.W * quaternion.Z + quaternion.X * quaternion.Y);
    float cosy_cosp = 1 - 2 * (quaternion.Y * quaternion.Y + quaternion.Z * quaternion.Z);
    yaw = (float)System.Math.Atan2(siny_cosp, cosy_cosp);

    return new Vector3(pitch, yaw, roll);
  }
}
