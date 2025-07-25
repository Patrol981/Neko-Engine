using System.Collections.Concurrent;
using Dwarf.SignalR.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace Dwarf.SignalR.Services;

public class DwarfClientService<TDwarfClientId> : IDwarfClientService<TDwarfClientId> {
  public ConcurrentDictionary<ISingleClientProxy, TDwarfClientId> DwarfClients { get; init; } = [];
}