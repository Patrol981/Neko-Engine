namespace Neko.Coroutines;

public class CoroutineItem {
  public Task CoroutineTask { get; set; }
  public CancellationTokenSource TokenSource { get; private set; }

  public CoroutineItem(Task task) {
    CoroutineTask = task;
    TokenSource = new CancellationTokenSource();
  }

  public CoroutineItem() {
    CoroutineTask = null!;
    TokenSource = new CancellationTokenSource();
  }
}
