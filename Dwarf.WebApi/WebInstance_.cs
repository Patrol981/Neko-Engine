namespace Dwarf.WebApi;

public class WebInstance_ : IDisposable {
  private Thread? _webThread;

  public delegate void EndpointMapInvoker();
  public EndpointMapInvoker? OnMap;

  public void Init() {
    _webThread = new Thread(Run);
    _webThread.Start();
  }

  public void Run() {
    var builder = WebApplication.CreateSlimBuilder();

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    WebApplication = builder.Build();

    WebApplication.UseSwagger();
    WebApplication.UseSwaggerUI();
    WebApplication.UseStaticFiles();

    MapEndpoints();

    WebApplication.Run();
  }

  public virtual void MapEndpoints() {
    OnMap?.Invoke();
  }

  private async void Close() {
    if (WebApplication != null) {
      await WebApplication.DisposeAsync();
    }
    _webThread?.Join();
  }

  public void Dispose() {
    Close();
  }

  public WebApplication? WebApplication { get; private set; }
}
