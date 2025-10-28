namespace Neko.WebApi.Endpoints;

public interface IEndpoint {
  public void DefineEndpoints(WebApplication app);
  public void DefineServices(IServiceCollection services);
}