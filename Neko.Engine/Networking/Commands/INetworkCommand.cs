using Neko.EntityComponentSystem;

namespace Neko.Networking.Commands;

public interface INetworkCommand {
  Task SetupListeners();
  Task Send(Entity target);
}