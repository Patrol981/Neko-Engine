using System.Text;
using Dwarf.EntityComponentSystem;
using Dwarf.Extensions.Logging;

namespace Dwarf.Networking.Commands;

public struct TransformData {
  public string Player;
  public float[] PositionXYZ;
}

public class TransformCommand(
  Application app,
  SignalRClientSystem client
) : NetworkCommandBase<TransformData>(app, client) {
  public override Task SetupListeners() {
    _client.RegisterEvent<object[]>(EventConstants.GET_TRANSFORM, (tranforms) => {
      Logger.Info("GET TRANSFORM");

      if (tranforms is not TransformData[] transformData) return;

      var networkObjects = _app.GetEntitiesEnumerable()
        .Where(x => x.HasComponent<NetworkComponent>())
        .ToDictionary(x => x.GetComponent<NetworkComponent>().NetworkId);

      for (int i = 0; i < transformData.Length; i++) {
        var uuid = Guid.Parse(transformData[i].Player);
        var targetTransform = networkObjects[uuid].GetComponent<Transform>();

        targetTransform.Position.X = transformData[i].PositionXYZ[0];
        targetTransform.Position.Y = transformData[i].PositionXYZ[1];
        targetTransform.Position.Z = transformData[i].PositionXYZ[2];
      }
    });

    return Task.CompletedTask;
  }

  public override async Task Send(Entity target) {
    var transform = target.GetComponent<Transform>().ToStringMsg();

    await _client.Send(EventConstants.SEND_TRANSFORM, transform);
  }
}

public static class TransformCommandHelper {
  public static TransformData ToTransformData(this Transform transform) {
    return new TransformData {
      Player = transform.Owner.GetComponent<NetworkComponent>().NetworkId.ToString(),
      PositionXYZ = [transform.Position.X, transform.Position.Y, transform.Position.Z]
    };
  }

  public static string ToStringMsg(this Transform transform) {
    return new StringBuilder()
      .Append(transform.Owner.GetComponent<NetworkComponent>().NetworkId.ToString())
      .Append('/')
      .Append(transform.Position.X)
      .Append('/')
      .Append(transform.Position.Y)
      .Append('/')
      .Append(transform.Position.Z)
      .ToString();
  }
}