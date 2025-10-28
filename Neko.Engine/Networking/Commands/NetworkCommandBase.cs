using Neko.EntityComponentSystem;

namespace Neko.Networking.Commands;

public abstract class NetworkCommandBase<T> : INetworkCommand {
  protected readonly Application _app;
  protected readonly SignalRClientSystem _client;

  protected NetworkCommandBase(Application app, SignalRClientSystem client) {
    _app = app;
    _client = client;
    SetupListeners();
  }

  public abstract Task SetupListeners();
  public abstract Task Send(Entity target);
}