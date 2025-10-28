using Neko.EntityComponentSystem;

namespace Neko.Networking;

public class NetworkComponent : INetworkObject {
  public Guid NetworkId { get; init; }

  public NetworkComponent() {
    NetworkId = Guid.NewGuid();
  }
}