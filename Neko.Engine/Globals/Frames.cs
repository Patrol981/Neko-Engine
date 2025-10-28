namespace Neko.Globals;

public static class Frames {
  // private static int s_frameCount = 0;
  // private static double s_prevTime = glfwGetTime();
  private static DateTime s_startTime;
  private static DateTime s_endTime;
  // private static double s_frameRate = 0.0f;

  public static double GetFramesDelta() {
    var lastUpdate = Time.DeltaTimeRender;
    return lastUpdate;
  }

  public static double GetFramesDeltaMain() {
    var lastUpdate = Time.DeltaTime;
    return lastUpdate;
  }

  public static double GetFrames() {
    var frame = (s_startTime - s_endTime).TotalMilliseconds;
    // return Time.DeltaTime;
    return MathF.Round((float)frame, 5, MidpointRounding.ToZero);
  }

  public static void TickStart() {
    s_startTime = DateTime.UtcNow;
  }

  public static void TickEnd() {
    s_endTime = DateTime.UtcNow;
  }

  /*
  public static double GetFrames() {
    double currentTime = glfwGetTime();
    s_frameCount++;
    if (currentTime - s_prevTime >= 1.0) {
      s_frameRate = s_frameCount;
      s_frameCount = 0;
      s_prevTime = currentTime;
    }
    return s_frameRate;
  }
  */
}
