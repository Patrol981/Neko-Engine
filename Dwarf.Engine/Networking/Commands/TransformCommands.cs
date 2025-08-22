using Dwarf.EntityComponentSystem;

namespace Dwarf.Networking.Commands;

public struct TransformData {
  public string[] Players;
  public float[] PositionsXYZ;
}

public class TransformCommands(
  Application app,
  SignalRClientSystem client
) : NetworkCommandBase(app, client) {
  public override void SetupListeners() {
    _client.RegisterEvent<TransformData>(EventConstants.GET_TRANSFORM, (tranforms) => {
      if (tranforms is not TransformData transformData) return;

      var networkObjects = _app.GetEntitiesEnumerable()
        .Where(x => x.HasComponent<NetworkComponent>())
        .ToArray();


    });
  }
}