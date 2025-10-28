using System.Collections.Concurrent;
using System.Numerics;
using System.Text.Json.Serialization;
using Neko.SignalR.Data;
using Neko.SignalR.Globals;
using Neko.SignalR.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace Neko.SignalR.Hubs;

[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(KeyValuePair<ISingleClientProxy, string>))]
[JsonSerializable(typeof(List<KeyValuePair<ISingleClientProxy, string>>))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(Vector3))]
[JsonSerializable(typeof(NekoPackage))]
[JsonSerializable(typeof(ConcurrentDictionary<Guid, NekoPackage>))]
public partial class NekoHubJsonSerializerContext : JsonSerializerContext { }

public partial class NekoHub : Hub {
  public override Task OnDisconnectedAsync(Exception? exception) {
    NekoHubData.NekoClients.Remove(Context.ConnectionId, out var _);
    return base.OnDisconnectedAsync(exception);
  }

  public override Task OnConnectedAsync() {
    NekoHubData.NekoClients.TryAdd(Context.ConnectionId, default);
    return base.OnConnectedAsync();
  }

  [HubMethodName(EventConstants.SEND_MSG)]
  public async Task SendMsg(string user, string message) {
    await Clients.Others.SendAsync(EventConstants.GET_MSG, user, message);
  }

  [HubMethodName(EventConstants.SEND_TRANSFORM)]
  public async Task SendTransform(string data) {
    // uuid/x/y/z
    var dataArray = data.Split('/');

    if (NekoHubData.NekoClients.ContainsKey(Context.ConnectionId)) {
      var NekoPackage = new NekoPackage();
      NekoPackage.Uuid = dataArray[0];
      NekoPackage.Position.X = float.Parse(dataArray[1]);
      NekoPackage.Position.Y = float.Parse(dataArray[2]);
      NekoPackage.Position.Z = float.Parse(dataArray[3]);

      NekoHubData.NekoClients[Context.ConnectionId] = NekoPackage;
    } else {
      Console.WriteLine($"User {Context.ConnectionId} does not appear in dictionary");
    }

    var toSend = NekoHubData.NekoClients.StringifyData();
    await Clients.Others.SendAsync(EventConstants.GET_TRANSFORM, toSend);
  }
}