namespace Neko.WebApi.Endpoints;

public static class EndpointExtensions {
  public static void AddEndpoints(this IServiceCollection services, params IEndpoint[] endpoints) {
    if (endpoints is null || endpoints.Length == 0) return;

    foreach (var endpoint in endpoints) {
      endpoint.DefineServices(services);
    }

    services.AddSingleton<IReadOnlyCollection<IEndpoint>>(endpoints);
  }

  public static void AddEndpoint<T>(this IServiceCollection services)
    where T : IEndpoint, new()
    => services.AddEndpoints(new T());

  public static void UseEndpoints(this WebApplication app) {
    var endpoints = app.Services.GetRequiredService<IReadOnlyCollection<IEndpoint>>();
    foreach (var endpoint in endpoints) {
      endpoint.DefineEndpoints(app);
    }
  }
}
