using System.Numerics;
using System.Text.Json.Serialization;
using Dwarf.SignalR.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace Dwarf.SignalR.Hubs;

[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(KeyValuePair<ISingleClientProxy, string>))]
[JsonSerializable(typeof(List<KeyValuePair<ISingleClientProxy, string>>))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(Vector3))]
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
  public async Task SendTransform(string user, string position) {
    await Clients.Others.SendAsync(EventConstants.GET_TRANSFORM, user, position);
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