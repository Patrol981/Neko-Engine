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
  private IDwarfClientService<string> _dwarfClientService;

  public DwarfHub(IDwarfClientService<string> dwarfClientService) : base() {
    _dwarfClientService = dwarfClientService;
  }

  [HubMethodName(EventConstants.SEND_MSG)]
  public async Task SendMsg(string user, string message) {
    await Clients.All.SendAsync(EventConstants.GET_MSG, user, message);
  }

  [HubMethodName(EventConstants.SEND_TRANSFORM)]
  public async Task SendTransform(string data) {
    // Console.WriteLine($"Received data from {data}");

    // uuid/x/y/z
    var dataArray = data.Split('/');

    var uuid = Guid.Parse(dataArray[0]);
    if (DwarfHubData.DwarfClients.ContainsKey(uuid)) {
      var dwarfPackage = new DwarfPackage();
      dwarfPackage.Uuid = dataArray[0];
      dwarfPackage.Position.X = float.Parse(dataArray[1]);
      dwarfPackage.Position.Y = float.Parse(dataArray[2]);
      dwarfPackage.Position.Z = float.Parse(dataArray[3]);

      DwarfHubData.DwarfClients[uuid] = dwarfPackage;

      // Console.WriteLine($"Updated package data: {dwarfPackage.Uuid} {dwarfPackage.Position}");
    } else {
      var newPackage = new DwarfPackage {
        Uuid = dataArray[0],
        Position = new() {
          X = float.Parse(dataArray[1]),
          Y = float.Parse(dataArray[2]),
          Z = float.Parse(dataArray[3])
        }
      };

      DwarfHubData.DwarfClients.TryAdd(Guid.Parse(newPackage.Uuid), newPackage);

      // Console.WriteLine($"Added new package data: {newPackage.Uuid} {newPackage.Position}");
    }

    var toSend = DwarfHubData.DwarfClients.StringifyData();
    foreach (var ts in toSend) {
      Console.WriteLine($"Resending data {ts}");
    }
    await Clients.All.SendAsync(EventConstants.GET_TRANSFORM, toSend);
  }

  [HubMethodName(EventConstants.SEND_INIT)]
  public async Task SendInit(string myUUID) {
    Console.WriteLine($"Incomming connection {myUUID}");
    _dwarfClientService.DwarfClients.TryAdd(Clients.Caller, myUUID);
    var uuidToSend = _dwarfClientService.DwarfClients
      .Where(x => x.Value != myUUID)
      .Select(x => x.Value)
      .ToArray();
    await Clients.All.SendAsync(
      EventConstants.GET_INIT,
      myUUID,
      _dwarfClientService.DwarfClients.Values.ToArray()
    );
  }
}