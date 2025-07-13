using Dwarf.Extensions.Logging;
using Dwarf.SignalR.Hubs;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Dwarf.Networking;

public class SignalRClientSystem : IDisposable {
  public HubConnection? Connection { get; private set; }

  public SignalRClientSystem() {
    // _connection = Application.Instance.
  }

  public async Task<Task> Connect(string url) {
    try {
      Connection = new HubConnectionBuilder()
        .WithUrl($"{url}/chathub")
        .WithAutomaticReconnect()
        .AddJsonProtocol(options => {
          options.PayloadSerializerOptions
            .TypeInfoResolverChain
            .Add(DwarfSignalRJsonSerializerContext.Default);
        })
        .Build();
      await Connection.StartAsync();
      Logger.Info($"Connected to {url}/chathub");
    } catch (Exception ex) {
      Logger.Error(ex.Message);
      throw;
    }

    return Task.CompletedTask;
  }

  public async Task<Task> Send(string sender, string message) {
    if (Connection == null || Connection.State == HubConnectionState.Disconnected) {
      return Task.CompletedTask;
    }

    await Connection.InvokeAsync("SendMessage", sender, message);

    return Task.CompletedTask;
  }

  public void Dispose() {
    Connection?.DisposeAsync().AsTask().Wait();

    GC.SuppressFinalize(this);
  }
}