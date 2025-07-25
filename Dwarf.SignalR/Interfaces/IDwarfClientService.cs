using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace Dwarf.SignalR.Interfaces;

public interface IDwarfClientService<TDwarfClientId> {
  public ConcurrentDictionary<ISingleClientProxy, TDwarfClientId> DwarfClients { get; }
}