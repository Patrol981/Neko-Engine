// namespace Neko.Networking.Commands;

// public class NetworkCommandBuilder {
//   private readonly Application _app;

//   public List<INetworkCommand> NetworkCommands { get; private set; } = [];

//   public NetworkCommandBuilder(Application app) {
//     _app = app;
//   }

//   public NetworkCommandBuilder AddTransformCommand() {
//     var nt = new TransformCommand(_app, _app.Systems.NetClientSystem);
//     NetworkCommands.Add(nt);
//     return this;
//   }

//   public INetworkCommand[] Build() {
//     return [.. NetworkCommands];
//   }
// }