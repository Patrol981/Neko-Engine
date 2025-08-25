using System.Collections.Concurrent;
using System.Numerics;
using System.Text.Json.Serialization;
using Dwarf.SignalR.Data;
using Dwarf.SignalR.Globals;
using Dwarf.SignalR.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace Dwarf.SignalR.Hubs;

[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(KeyValuePair<ISingleClientProxy, string>))]
[JsonSerializable(typeof(List<KeyValuePair<ISingleClientProxy, string>>))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(Vector3))]
[JsonSerializable(typeof(DwarfPackage))]
[JsonSerializable(typeof(ConcurrentDictionary<Guid, DwarfPackage>))]
public partial class DwarfHubJsonSerializerContext : JsonSerializerContext { }

public partial class DwarfHub : Hub {
  public override Task OnDisconnectedAsync(Exception? exception) {
    DwarfHubData.DwarfClients.Remove(Context.ConnectionId, out var _);
    return base.OnDisconnectedAsync(exception);
  }

  public override Task OnConnectedAsync() {
    DwarfHubData.DwarfClients.TryAdd(Context.ConnectionId, default);
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

    if (DwarfHubData.DwarfClients.ContainsKey(Context.ConnectionId)) {
      var dwarfPackage = new DwarfPackage();
      dwarfPackage.Uuid = dataArray[0];
      dwarfPackage.Position.X = float.Parse(dataArray[1]);
      dwarfPackage.Position.Y = float.Parse(dataArray[2]);
      dwarfPackage.Position.Z = float.Parse(dataArray[3]);

      DwarfHubData.DwarfClients[Context.ConnectionId] = dwarfPackage;
    } else {
      Console.WriteLine($"User {Context.ConnectionId} does not appear in dictionary");
    }

    var toSend = DwarfHubData.DwarfClients.StringifyData();
    await Clients.Others.SendAsync(EventConstants.GET_TRANSFORM, toSend);
  }
}