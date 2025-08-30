using Dwarf.EntityComponentSystem;

namespace Dwarf.Networking;

public class NetworkComponent : INetworkObject {
  public Guid NetworkId { get; init; }

  public NetworkComponent() {
    NetworkId = Guid.NewGuid();
  }
}