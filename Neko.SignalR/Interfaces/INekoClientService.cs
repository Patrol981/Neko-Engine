using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace Neko.SignalR.Interfaces;

public interface INekoClientService<TNekoClientId>
{
  public ConcurrentDictionary<ISingleClientProxy, TNekoClientId> NekoClients { get; }
}