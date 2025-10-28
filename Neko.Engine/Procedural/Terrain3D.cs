using System.Numerics;

using Neko.EntityComponentSystem;
using Neko.Math;
using Neko.Rendering;
using Neko.Rendering.Renderer3D;

namespace Neko.Procedural;

public class Terrain3D {
  const int HEIGHT = 512;
  const int WIDTH = 512;

  public Entity Owner { get; init; }

  private readonly double[,] _points;
  private readonly Application _app = default!;

  private Vector2 _size = Vector2.Zero;
  private string _texturePath = string.Empty;
  private int _repX;
  private int _repY;

  public Terrain3D(Entity owner) {
    Owner = owner;
    _points = new double[HEIGHT, WIDTH];
  }

  public Terrain3D(Entity owner, Application app) {
    Owner = owner;
    _app = app;
    _points = new double[HEIGHT, WIDTH];
  }

  public void Setup(Vector2 size, string? texturePath = default, int repX = 15, int repY = 15) {
    _size = size;
    _repX = repX;
    _repY = repY;
    _texturePath = texturePath != null ? texturePath : "./Resources/Textures/base/no_texture.png";
    var mesh = Generate(_app);
    var guid = Guid.NewGuid();
    _app.Meshes.TryAdd(guid, mesh);
    SetupTexture(_app);

    Owner.AddDrawable3D(new MeshRenderer(Owner, _app, _app.Device, _app.Renderer));
    Owner.GetDrawable3D()?.AddLinearNode(new Node(_app, Owner.Id) { MeshGuid = guid });
    Owner.GetDrawable3D()?.Init(_app.Meshes);
    Owner.GetDrawable3D()?.BindToTexture(_app.TextureManager, _texturePath, _app.Meshes);

    // Owner!.AddComponent(new MeshRenderer(_app.Device, _app.Renderer));
    // Owner!.GetComponent<MeshRenderer>().AddLinearNode(new Node() { Mesh = mesh, });
    // Owner!.GetComponent<MeshRenderer>().Init();
    // Owner!.GetComponent<MeshRenderer>().BindToTexture(_app.TextureManager, _texturePath);
  }

  private Mesh Generate(Application app) {
    var rand = new Random();

    for (int y = 0; y < HEIGHT; y++) {
      for (int x = 0; x < WIDTH; x++) {
        double nx = x / WIDTH - 0.5;
        double ny = y / HEIGHT - 0.5;
        _points[x, y] = Noise.Perlin((float)nx, (float)ny);
      }
    }

    var mesh = Primitives.CreatePlanePrimitive(
      new(0, 0, 0),
      new(100, 100),
      new(_size.X, _size.Y),
      new(_repX, _repY)
    );

    // ApplyPerlinNoiseToMesh(ref mesh, _points, _size, new(WIDTH, HEIGHT));

    return mesh;
  }

  private async void SetupTexture(Application app) {
    Application.Mutex.WaitOne();
    await app.TextureManager.AddTextureLocal(_texturePath);
    Application.Mutex.ReleaseMutex();
  }

  private static void ApplyPerlinNoiseToMesh(
    ref Mesh mesh,
    double[,] noiseMap,
    Vector2 worldSize,
    Vector2 numVertices
  ) {
    var vertices = mesh.Vertices;

    float xStep = worldSize.X / (numVertices.X - 1);
    float yStep = worldSize.Y / (numVertices.Y - 1);

    for (int y = 0; y < numVertices.Y; y++) {
      for (int x = 0; x < numVertices.X; x++) {
        int index = x + y * (int)numVertices.X;
        var noiseValue = noiseMap[x, y]; // Get noise value from the map

        // Update Y coordinate of the vertex
        vertices[index].Position.Y += (float)noiseValue;
      }
    }

  }
}
