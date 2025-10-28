namespace Neko.Coroutines;

public class WaitForSeconds : YieldInstruction {
  public float Seconds { get; private set; }

  public WaitForSeconds(float seconds) {
    Seconds = seconds;
  }
}
