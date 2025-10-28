using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR;

namespace Neko.SignalR.Hubs;

[JsonSerializable(typeof(string))]
public partial class ChatHubJsonSerializerContext : JsonSerializerContext { }

public class ChatHub : Hub
{
  [HubMethodName(EventConstants.SEND_MSG)]
  public async Task SendMsg(string user, string message)
  {
    await Clients.All.SendAsync(EventConstants.GET_MSG, user, message);
  }
}