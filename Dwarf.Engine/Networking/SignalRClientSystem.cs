using System.Collections.Concurrent;
using Dwarf.Extensions.Logging;
using Dwarf.SignalR.Hubs;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Dwarf.Networking;

public delegate void NetworkSenderEvent(object? sender, object? msg);
public delegate void NetworkEvent(object? msg);

public class SignalRClientSystem : IDisposable {
  private readonly ConcurrentDictionary<string, HubConnection> _connections = [];

  public async Task<bool> Connect(string url, string hubName = HubNames.DEFAULT) {
    if (_connections.ContainsKey(hubName)) {
      Logger.Error($"{hubName} alredy exists in the pool. Skipping");
      return false;
    }

    try {
      var result = _connections.TryAdd(hubName, new HubConnectionBuilder()
       .WithUrl($"{url}/{hubName}")
       .WithAutomaticReconnect()
       .AddJsonProtocol(options => {
         options.PayloadSerializerOptions
           .TypeInfoResolverChain
           .Add(DwarfHubJsonSerializerContext.Default);
         options.PayloadSerializerOptions
           .TypeInfoResolverChain
           .Add(ChatHubJsonSerializerContext.Default);
       })
       .Build()
      );
      if (result) {
        _connections.TryGetValue(hubName, out var connection);
        await connection!.StartAsync();
        Logger.Info($"Connected to {url}/{hubName}");
        return true;
      }
    } catch (Exception ex) {
      Logger.Error(ex.Message);
      throw;
    }

    return false;
  }

  public async Task Send(
    string eventName,
    object data,
    string hubName = HubNames.DEFAULT
  ) {
    var result = _connections.TryGetValue(hubName, out var connection);
    if (!result) {
      throw new KeyNotFoundException("Could not find given hub name");
    }

    if (connection == null || connection.State == HubConnectionState.Disconnected) {
      return;
    }

    await connection.InvokeAsync(eventName, data);

    return;
  }

  public async Task Send(
    string eventName,
    string args,
    string hubName = HubNames.DEFAULT
  ) {
    var result = _connections.TryGetValue(hubName, out var connection);
    if (!result) {
      throw new KeyNotFoundException("Could not find given hub name");
    }

    if (connection == null || connection.State == HubConnectionState.Disconnected) {
      return;
    }

    await connection.InvokeAsync(eventName, args);

    return;
  }

  public async Task Send<T>(
    string eventName,
    string sender,
    T data,
    string hubName = HubNames.DEFAULT
  ) {
    var result = _connections.TryGetValue(hubName, out var connection);
    if (!result) {
      throw new KeyNotFoundException("Could not find given hub name");
    }

    if (connection == null || connection.State == HubConnectionState.Disconnected) {
      return;
    }

    await connection.InvokeAsync(eventName, sender, data);

    return;
  }

  public void RegisterEvent<T1, T2>(
    string eventName,
    NetworkSenderEvent networkEvent,
    string hubName = HubNames.DEFAULT
  ) {
    var result = _connections.TryGetValue(hubName, out var connection);
    if (!result) {
      throw new KeyNotFoundException("Could not find given hub name");
    }
    connection?.On<T1, T2>(eventName, (sender, msg) => {
      networkEvent.Invoke(sender, msg);
    });
  }

  public void RegisterEvent<T>(
    string eventName,
    NetworkEvent networkEvent,
    string hubName = HubNames.DEFAULT
  ) {
    var result = _connections.TryGetValue(hubName, out var connection);
    if (!result) {
      throw new KeyNotFoundException("Could not find given hub name");
    }
    connection?.On<T>(eventName, (msg) => {
      networkEvent.Invoke(msg);
    });
  }

  public void Dispose() {
    foreach (var conn in _connections.Values) {
      conn.DisposeAsync().AsTask().Wait();
    }

    GC.SuppressFinalize(this);
  }
}