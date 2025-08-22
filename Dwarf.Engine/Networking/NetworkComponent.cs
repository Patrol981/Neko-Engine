using Dwarf.EntityComponentSystem;

namespace Dwarf.Networking;

public class NetworkComponent : Component, INetworkObject {
  public Guid NetworkId { get; init; }

  public NetworkComponent() {
    NetworkId = Guid.NewGuid();
  }
}