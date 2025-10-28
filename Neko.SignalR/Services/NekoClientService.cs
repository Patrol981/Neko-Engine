using System.Collections.Concurrent;
using Neko.SignalR.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace Neko.SignalR.Services;

public class NekoClientService<TNekoClientId> : INekoClientService<TNekoClientId>
{
  public ConcurrentDictionary<ISingleClientProxy, TNekoClientId> NekoClients { get; init; } = [];
}