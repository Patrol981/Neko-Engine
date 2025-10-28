using Neko.SignalR.Hubs;
using Neko.SignalR.Interfaces;
using Neko.SignalR.Services;
using Microsoft.AspNetCore.SignalR;

namespace Neko.SignalR;

public class SignalRInstance : IAsyncDisposable
{
  public const string HTTP_URL = "http://*:4222";
  public const string HTTPS_URL = "https://*:4223";
  public const string CORS_NAME = "DEFAULT_CORS_POLICY_NAME";

  private readonly WebApplicationBuilder? _builder;
  private WebApplication? _app;

  public SignalRInstance()
  {
    _builder = WebApplication.CreateSlimBuilder();
    _builder.Services.Configure<JsonHubProtocolOptions>(options =>
    {
      options.PayloadSerializerOptions.TypeInfoResolverChain.Insert(0, NekoHubJsonSerializerContext.Default);
      options.PayloadSerializerOptions.TypeInfoResolverChain.Insert(1, ChatHubJsonSerializerContext.Default);
    });
    _builder.Services.AddTransient<INekoClientService<string>, NekoClientService<string>>();
    _builder.Services.AddSignalR();
    _builder.WebHost.UseUrls(HTTP_URL);
  }

  public void AddCorsOptions(params string[] origins)
  {
    _builder?.Services.AddCors(options =>
    {
      foreach (var origin in origins)
      {
        options.AddPolicy(name: CORS_NAME, builder =>
        {
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

  public void AllowAnyOrigin()
  {
    _builder?.Services.AddCors(options =>
    {
      options.AddPolicy(name: CORS_NAME, builder =>
      {
        builder.AllowAnyHeader();
        builder.AllowAnyMethod();
        builder.AllowAnyOrigin();
      });
    });
  }

  public void Run()
  {
    if (_builder == null)
    {
      return;
    }
    _app = _builder.Build();
    _app.UseRouting();
    _app.MapHub<NekoHub>($"/{HubNames.DEFAULT}");
    _app.MapHub<ChatHub>($"/{HubNames.CHAT_HUB}");
    _app.UseCors(CORS_NAME);
    _app.Run();
  }

  public async ValueTask DisposeAsync()
  {
    if (_app != null)
    {
      await _app.StopAsync();
      await _app.DisposeAsync().AsTask();
    }

    GC.SuppressFinalize(this);
  }
}