using System.Numerics;
using Neko.AbstractionLayer;
using Neko.EntityComponentSystem;
using Neko.Extensions.Logging;
using Neko.Math;
using Neko.Rendering.Renderer2D.Components;
using Neko.Rendering.Renderer2D.Helpers;
using Neko.Rendering.Renderer2D.Interfaces;

namespace Neko.Rendering.Renderer2D.Models;

public class TilemapLayer : IDrawable2D {
  private readonly Application _app;
  private readonly Tilemap _parent;

  public Mesh LayerMesh { get; private set; } = null!;
  public ITexture LayerTexture { get; private set; } = null!;
  public TileInfo[,] Tiles { get; set; }
  public bool IsCollision { get; init; }
  public bool DescriptorBuilt => throw new NotImplementedException();
  public Entity Entity => _parent.Entity;
  public bool Active => _parent.Entity.Active;
  public ITexture Texture => LayerTexture;
  public ITexture[] SpriteSheet => [LayerTexture];
  public Vector2I SpriteSheetSize => new(1, 1);
  public int SpriteIndex => 0;
  public int SpriteCount => 1;
  public bool FlipX { get; set; }
  public bool FlipY { get; set; }
  public bool NeedPipelineCache => throw new NotImplementedException();
  public Mesh CollisionMesh => throw new NotImplementedException();
  public Mesh Mesh => LayerMesh;
  public Mesh[] Meshes => [LayerMesh];
  public bool HasMultipleMeshes => false;
  public IDrawable2D[] Children => [];
  public float LocalZDepth { get; set; }

  public ShaderInfo CustomShader { get; }

  public TilemapLayer(
    Application app,
    Tilemap parent,
    TileInfo[,] tiles,
    string path,
    bool isCollision,
    float zDepth
  ) {
    _app = app;
    _parent = parent;
    Tiles = tiles;
    IsCollision = isCollision;
    SetupTexture(path);
    LocalZDepth = zDepth;
  }

  public void SetTile(int x, int y, TileInfo tileInfo) {
    if (Tiles.IsWithinTilemap(x, y)) {
      Tiles[x, y].X = tileInfo.X;
      Tiles[x, y].Y = tileInfo.X;
      Tiles[x, y].TextureX = tileInfo.TextureX;
      Tiles[x, y].TextureY = tileInfo.TextureY;
      Tiles[x, y].IsNotEmpty = tileInfo.IsNotEmpty;
    } else {
      throw new IndexOutOfRangeException("Attempted to set tile outside of timemap range");
    }
  }

  public TileInfo? GetTile(int x, int y) {
    if (Tiles.IsWithinTilemap(x, y)) {
      return Tiles[x, y];
    }
    return null;
  }

  public void GenerateMesh() {
    LayerMesh = new(_app.Allocator, _app.Device);

    var vertices = new List<Vertex>();
    var indices = new List<uint>();

    float worldTileSize = 0.10f;

    for (uint y = 0; y < _parent.TilemapSize.Y; y++) {
      for (uint x = 0; x < _parent.TilemapSize.X; x++) {
        var tileInfo = Tiles[x, y];

        if (!tileInfo.IsNotEmpty) continue;

        float posX = x * worldTileSize;
        float posY = y * worldTileSize;

        // float uMin = tileInfo.TextureX * TileSize / atlasWidth;
        // float vMin = tileInfo.TextureY * TileSize / atlasHeight;
        // float uMax = (tileInfo.TextureX + 1) * TileSize / atlasWidth;
        // float vMax = (tileInfo.TextureY + 1) * TileSize / atlasHeight;

        // uMin = 0;
        // uMax = -1;
        // vMax = -1;
        // vMin = 0;

        if (tileInfo.HasUVCoords()) {
          var bottomLeft = new Vertex {
            Position = new Vector3(posX, posY, 0.0f),
            Uv = new Vector2(tileInfo.UMin, tileInfo.VMin),
            Color = new Vector3(1, 1, 1),
            Normal = new Vector3(1, 1, 1)
          };
          var bottomRight = new Vertex {
            Position = new Vector3(posX + worldTileSize, posY, 0.0f),
            Uv = new Vector2(tileInfo.UMax, tileInfo.VMin),
            Color = new Vector3(1, 1, 1),
            Normal = new Vector3(1, 1, 1)
          };
          var topRight = new Vertex {
            Position = new Vector3(posX + worldTileSize, posY + worldTileSize, 0.0f),
            Uv = new Vector2(tileInfo.UMax, tileInfo.VMax),
            Color = new Vector3(1, 1, 1),
            Normal = new Vector3(1, 1, 1)
          };
          var topLeft = new Vertex {
            Position = new Vector3(posX, posY + worldTileSize, 0.0f),
            Uv = new Vector2(tileInfo.UMin, tileInfo.VMax),
            Color = new Vector3(1, 1, 1),
            Normal = new Vector3(1, 1, 1)
          };

          uint baseIndex = (uint)vertices.Count;
          vertices.Add(bottomLeft);
          vertices.Add(bottomRight);
          vertices.Add(topRight);
          vertices.Add(topLeft);

          // Two triangles per quad, using counter-clockwise winding (adjust if needed).
          indices.Add(baseIndex + 0);
          indices.Add(baseIndex + 1);
          indices.Add(baseIndex + 2);

          indices.Add(baseIndex + 0);
          indices.Add(baseIndex + 2);
          indices.Add(baseIndex + 3);
        } else {
          var (uMin, uMax, vMin, vMax) = GetUVCoords(tileInfo);

          var bottomLeft = new Vertex {
            Position = new Vector3(posX, posY, 0.0f),
            Uv = new Vector2(uMin, vMin),
            Color = new Vector3(1, 1, 1),
            Normal = new Vector3(1, 1, 1)
          };
          var bottomRight = new Vertex {
            Position = new Vector3(posX + worldTileSize, posY, 0.0f),
            Uv = new Vector2(uMax, vMin),
            Color = new Vector3(1, 1, 1),
            Normal = new Vector3(1, 1, 1)
          };
          var topRight = new Vertex {
            Position = new Vector3(posX + worldTileSize, posY + worldTileSize, 0.0f),
            Uv = new Vector2(uMax, vMax),
            Color = new Vector3(1, 1, 1),
            Normal = new Vector3(1, 1, 1)
          };
          var topLeft = new Vertex {
            Position = new Vector3(posX, posY + worldTileSize, 0.0f),
            Uv = new Vector2(uMin, vMax),
            Color = new Vector3(1, 1, 1),
            Normal = new Vector3(1, 1, 1)
          };

          uint baseIndex = (uint)vertices.Count;
          vertices.Add(bottomLeft);
          vertices.Add(bottomRight);
          vertices.Add(topRight);
          vertices.Add(topLeft);

          // Two triangles per quad, using counter-clockwise winding (adjust if needed).
          indices.Add(baseIndex + 0);
          indices.Add(baseIndex + 1);
          indices.Add(baseIndex + 2);

          indices.Add(baseIndex + 0);
          indices.Add(baseIndex + 2);
          indices.Add(baseIndex + 3);
        }
      }
    }

    LayerMesh.Vertices = [.. vertices];
    LayerMesh.Indices = [.. indices];
  }

  public void SetupTexture(string path) {
    if (Application.ApplicationMode == ApplicationType.Headless) return;

    LayerTexture = _app.TextureManager.AddTextureLocal(path).Result;
  }

  private (float, float, float, float) GetUVCoords(TileInfo tileInfo) {
    int row = tileInfo.TextureY;
    int col = tileInfo.TextureX;

    float uvSize = 1.0f / _parent.TileSize;
    float uMin = col * uvSize;
    float vMin = 1.0f - (row + 1) * uvSize;
    float uMax = (col + 1) * uvSize;
    float vMax = 1.0f - row * uvSize;

    (vMax, vMin) = (vMin, vMax);

    return (uMin, uMax, vMin, vMax);
  }

  public void Dispose() {

  }

  public void BuildDescriptors(IDescriptorSetLayout descriptorSetLayout, IDescriptorPool descriptorPool) {
    throw new NotImplementedException();
  }

  public void CachePipelineLayout(object pipelineLayout) {
    throw new NotImplementedException();
  }

  public Task Bind(nint commandBuffer, uint index) {
    throw new NotImplementedException();
  }

  public Task Draw(nint commandBuffer, uint index = 0, uint firstInstance = 0) {
    throw new NotImplementedException();
  }

  public object Clone() {
    throw new NotImplementedException();
  }

  public void SetCustomShader(ShaderInfo shaderInfo) {
    throw new NotImplementedException();
  }

  public void SetShaderTextureInfo(Guid textureId) {
    throw new NotImplementedException();
  }
}