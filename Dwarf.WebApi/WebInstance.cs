using System.Text.Json.Serialization.Metadata;
using Dwarf.WebApi.Endpoints;

namespace Dwarf.WebApi;

public class WebInstance : IAsyncDisposable {
  public const string HTTP_URL = "http://*:4212";
  public const string HTTPS_URL = "https://*:4213";

  private readonly WebApplicationBuilder? _builder;
  private WebApplication? _app;

  public WebInstance(
    IJsonTypeInfoResolver[] expectedTypes
  ) {
    _builder = WebApplication.CreateSlimBuilder();
    _builder.Services.ConfigureHttpJsonOptions(options => {
      int index = 0;
      foreach (var expectedType in expectedTypes) {
        options.SerializerOptions.TypeInfoResolverChain.Insert(index, expectedType);
        index++;
      }
    });
    _builder.WebHost.UseUrls(HTTP_URL);
  }

  public void AddEndpoints(params IEndpoint[] endpoints) {
    _builder?.Services.AddEndpoints(endpoints);
  }

  public void Run() {
    if (_builder == null) {
      return;
    }

    _app = _builder.Build();
    _app.UseRouting();
    _app.UseEndpoints();
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