using Microsoft.AspNetCore.SignalR;

namespace Dwarf.SignalR.Hubs;

public partial class DwarfHub : Hub {
  [HubMethodName(EventConstants.REMOTE_SEND_INIT)]
  public async Task RemoteSendInit() {
    await Clients.Caller.SendAsync(
      EventConstants.REMOTE_GET_INIT,
      "Hello From Server!"
    );
  }

  public async Task RemoteSendSpawnEntityEvent(string entityName, float x, float y, float z) {
    await Clients.Others.SendAsync(
      "",
      entityName,
      x,
      y,
      z
    );
  }
}