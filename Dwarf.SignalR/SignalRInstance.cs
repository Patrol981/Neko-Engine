using Dwarf.SignalR.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Dwarf.SignalR;

public class SignalRInstance : IAsyncDisposable {
  private readonly WebApplicationBuilder? _builder;
  private WebApplication? _app;

  public SignalRInstance() {
    _builder = WebApplication.CreateSlimBuilder();
    _builder.Services.Configure<JsonHubProtocolOptions>(options => {
      options.PayloadSerializerOptions.TypeInfoResolverChain.Insert(0, DwarfSignalRJsonSerializerContext.Default);
    });
    _builder.Services.AddSignalR();
  }

  public void Run() {
    if (_builder == null) {
      return;
    }
    _app = _builder.Build();
    _app.UseRouting();
    _app.MapHub<ChatHub>("/chathub");
    _app.Run();
  }

  public async ValueTask DisposeAsync() {
    if (_app != null) {
      await _app.StopAsync();
      await _app.DisposeAsync().AsTask();
    }

    GC.SuppressFinalize(this);
  }
}