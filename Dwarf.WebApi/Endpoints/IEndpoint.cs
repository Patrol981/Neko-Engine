namespace Dwarf.WebApi.Endpoints;

public interface IEndpoint {
  public void DefineEndpoints(WebApplication app);
  public void DefineServices(IServiceCollection services);
}