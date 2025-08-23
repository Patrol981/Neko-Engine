using Dwarf.EntityComponentSystem;

namespace Dwarf.Networking.Commands;

public interface INetworkCommand {
  Task SetupListeners();
  Task Send(Entity target);
}