
using Neko.Networking.WebApi.Interfaces;
using Neko.Networking.WebApi.Services;
using Neko.WebApi.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http.HttpResults;
using Neko.Rendering;
using Neko.Networking.WebApi.Models;

namespace Neko.Networking.WebApi.Endpoints;

public class CommonEndpoints : IEndpoint {
  public void DefineEndpoints(WebApplication app) {
    app.MapGet("Hello", () => "Hello World!");

    app.MapGet("Level/Mesh/2D", Get2DLevelMesh);
  }

  public void DefineServices(IServiceCollection services) {
    services.AddSingleton<IMeshService, MeshService>();
  }

  public static MeshResponse Get2DLevelMesh(IMeshService meshService) {
    return meshService.Get2DLevelMesh();
  }
}