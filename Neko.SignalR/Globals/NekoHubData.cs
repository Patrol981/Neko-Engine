using System.Collections.Concurrent;
using System.Text;
using Neko.SignalR.Data;
using Microsoft.AspNetCore.SignalR;

namespace Neko.SignalR.Globals;

public static class NekoHubData
{
  public static ConcurrentDictionary<string, NekoPackage> NekoClients { get; private set; } = [];

  public static string[] StringifyData(this ConcurrentDictionary<string, NekoPackage> clients)
  {
    var arr = new string[clients.Count];
    int i = 0;
    foreach (var client in clients.Values)
    {
      // 0 = uuid
      // 1 = X
      // 2 = Y
      // 3 = Z
      var sb = new StringBuilder();
      sb.AppendFormat(
        "{0}/{1}/{2}/{3}",
        client.Uuid,
        client.Position.X,
        client.Position.Y,
        client.Position.Z
      );
      arr[i] = sb.ToString();
      i++;
    }

    return arr;
  }
}