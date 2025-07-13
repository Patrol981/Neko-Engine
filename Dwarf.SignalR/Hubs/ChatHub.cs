using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR;

namespace Dwarf.SignalR.Hubs;

[JsonSerializable(typeof(string))]
public partial class DwarfSignalRJsonSerializerContext : JsonSerializerContext { }
public class ChatHub : Hub {
  public async Task SendMessage(string user, string message) {
    await Clients.All.SendAsync("ReceiveMessage", user, message);
  }
}