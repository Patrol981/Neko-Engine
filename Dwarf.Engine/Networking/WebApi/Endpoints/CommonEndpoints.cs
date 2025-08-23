
using Dwarf.Networking.WebApi.Interfaces;
using Dwarf.Networking.WebApi.Services;
using Dwarf.WebApi.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http.HttpResults;
using Dwarf.Rendering;
using Dwarf.Networking.WebApi.Models;

namespace Dwarf.Networking.WebApi.Endpoints;

public class CommonEndpoints : IEndpoint {
  public void DefineEndpoints(WebApplication app) {
    app.MapGet("Hello", () => "Hello World!");

    app.MapGet("Level/Mesh/2D", Get2DLevelMesh);
  }

  public void DefineServices(IServiceCollection services) {
    services.AddSingleton<IMeshService, MeshService>();
  }

  public static VertexResponse[] Get2DLevelMesh(IMeshService meshService) {
    return meshService.Get2DLevelMesh();
  }
}