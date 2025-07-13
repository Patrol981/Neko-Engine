using Dwarf.Extensions.Logging;
using Dwarf.SignalR;

namespace Dwarf.Networking;

public class SignalRSystem : IDisposable {
  private readonly Application? _application;
  private readonly SignalRInstance? _netInstance;
  private Thread? _netThread;

  public SignalRSystem(Application app) {
    _application = app;
    _netInstance = new SignalRInstance();
    Logger.Info("[SYSTEMS] SignalR System created");

    _netThread = new Thread(Run) {
      Name = "SignalR Thread",
      IsBackground = true,
      Priority = ThreadPriority.BelowNormal,
    };
    _netThread.Start();
  }

  private void Run() {
    if (_netThread == null) return;

    Logger.Info($"[SYSTEMS] SignalR System Running on Thread {_netThread.Name} - {_netThread.ManagedThreadId}");
    _netInstance?.Run();
  }

  public void Dispose() {
    _netInstance?.DisposeAsync().AsTask().Wait();
    _netThread?.Join();

    GC.SuppressFinalize(this);
  }
}