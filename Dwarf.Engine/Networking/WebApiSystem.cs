using Dwarf.EntityComponentSystem;
using Dwarf.Extensions.Logging;
using Dwarf.Networking.WebApi;
using Dwarf.Networking.WebApi.Endpoints;
using Dwarf.Utils;
using Dwarf.WebApi;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Dwarf.Networking;

public class WebApiSystem : IDisposable {
  private readonly Application? _application;
  private readonly WebInstance? _webInstance;
  private Thread? _webThread;

  public class RotationData {
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
  }

  public class TranslationData {
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
  }

  public WebApiSystem(Application app) {
    var commonTypes = CommonJsonTypesProvider.Provide();

    _application = app;
    _webInstance = new WebInstance([.. commonTypes]);
    _webInstance.AddEndpoints([new CommonEndpoints()]);

    _webThread = new Thread(Run) {
      Name = "WebApi Thread",
      IsBackground = true,
      Priority = ThreadPriority.BelowNormal
    };
    _webThread.Start();
  }

  private void Run() {
    if (_webThread == null) return;

    Logger.Info($"[SYSTEMS] WebApi System Running on Thread {_webThread.Name} - {_webThread.ManagedThreadId}");
    _webInstance?.Run();
  }

  /*
  public void MapEndpoints() {
    if (_webInstance == null || _webInstance.WebApplication == null) return;

    var api = _webInstance.WebApplication.MapGroup("/api");

    api.MapGet("/ping", () => {
      return Results.Ok("pong");
    });

    api.MapPost("/model", async (HttpContext context) => {
      var form = await context.Request.ReadFormAsync();
      var file = form.Files.GetFile("file");

      if (file == null || file.Length == 0 || Path.GetExtension(file.FileName).ToLower() != ".glb") {
        return Results.BadRequest("Please upload a valid .glb file.");
      }

      var filePath = Path.Combine(DwarfPath.AssemblyDirectory, "Resources", file.FileName);

      using (var stream = new FileStream(filePath, FileMode.Create)) {
        await file.CopyToAsync(stream);
      }

      _application?.Mutex.WaitOne();
      var postModel = new Entity {
        Name = Guid.NewGuid().ToString()
      };
      postModel.AddTransform();
      postModel.AddMaterial();
      // postModel.AddRigidbody(PrimitiveType.Box, new(.5f, 1f, .5f), new(0, -1f, 0), true, false);
      postModel.AddModel(filePath, 0);
      postModel.GetComponent<Rendering.Renderer3D.Animations.AnimationController>().PlayFirstAnimation();

      _application?.AddEntity(postModel);
      _application?.Mutex.ReleaseMutex();

      return Results.Ok();
    }).DisableAntiforgery();

    api.MapPost("/rotate", async (HttpContext context) => {
      var id = context.Request.Query["id"].ToString();

      if (!Guid.TryParse(id, out var guid)) {
        return Results.BadRequest("Invalid ID format.");
      }

      var rotation = await context.Request.ReadFromJsonAsync<RotationData>();
      if (rotation == null) {
        return Results.BadRequest("Invalid rotation data.");
      }

      var target = _application!.GetEntity(Guid.Parse(id));

      if (target == null) {
        return Results.NotFound();
      }

      target.GetComponent<Transform>().Rotation.X = rotation.X;
      target.GetComponent<Transform>().Position.Y = rotation.Y;
      target.GetComponent<Transform>().Position.Z = rotation.Z;

      return Results.Ok();
    });

    api.MapPost("/translate", async (HttpContext context) => {
      var id = context.Request.Query["id"].ToString();

      if (!Guid.TryParse(id, out var guid)) {
        return Results.BadRequest("Invalid ID format.");
      }

      var transform = await context.Request.ReadFromJsonAsync<TranslationData>();
      if (transform == null) {
        return Results.BadRequest("Invalid translation data.");
      }

      var target = _application!.GetEntity(guid);
      if (target == null) {
        return Results.NotFound();
      }

      target.GetComponent<Transform>().Position.X = transform.X;
      target.GetComponent<Transform>().Position.Y = transform.Y;
      target.GetComponent<Transform>().Position.Z = transform.Z;

      return Results.Ok();
    });
  }
  */

  public void Dispose() {
    _webInstance?.DisposeAsync().AsTask().Wait();
    _webThread?.Join();

    GC.SuppressFinalize(this);
  }
}
