using System.Text.Json.Serialization.Metadata;
using Neko.WebApi.Endpoints;

namespace Neko.WebApi;

public class WebInstance : IAsyncDisposable {
  public const string HTTP_URL = "http://*:4212";
  public const string HTTPS_URL = "https://*:4213";
  public const string CORS_NAME = "DEFAULT_CORS_POLICY_NAME";

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

  public void AddCorsOptions(params string[] origins) {
    _builder?.Services.AddCors(options => {
      foreach (var origin in origins) {
        options.AddPolicy(name: CORS_NAME, builder => {
          builder.WithOrigins(
            $"http://{origin}",
            $"https://{origin}"
          )
          .AllowCredentials()
          .AllowAnyHeader()
          .AllowAnyMethod();
        });
      }
    });
  }

  public void AllowAnyOrigin() {
    _builder?.Services.AddCors(options => {
      options.AddPolicy(name: CORS_NAME, builder => {
        builder.AllowAnyHeader();
        builder.AllowAnyMethod();
        builder.AllowAnyOrigin();
      });
    });
  }

  public void Run() {
    if (_builder == null) {
      return;
    }

    _app = _builder.Build();
    _app.UseRouting();
    _app.UseCors(CORS_NAME);
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