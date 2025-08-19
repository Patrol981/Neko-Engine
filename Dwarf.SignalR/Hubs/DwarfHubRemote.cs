using Microsoft.AspNetCore.SignalR;

namespace Dwarf.SignalR.Hubs;

public partial class DwarfHub : Hub {
  [HubMethodName(EventConstants.REMOTE_SEND_INIT)]
  public async Task RemoteSendInit(CancellationToken? cancellationToken) {
    await Clients.Caller.SendAsync(
      EventConstants.REMOTE_GET_INIT,
      "Hello From Server!",
      cancellationToken
    );
  }
}