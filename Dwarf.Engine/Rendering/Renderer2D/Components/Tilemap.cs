using System.Numerics;
using Dwarf;
using Dwarf.AbstractionLayer;
using Dwarf.EntityComponentSystem;
using Dwarf.Extensions.Logging;
using Dwarf.Math;
using Dwarf.Rendering.Renderer2D.Helpers;
using Dwarf.Rendering.Renderer2D.Interfaces;
using Dwarf.Rendering.Renderer2D.Models;
using Dwarf.Vulkan;
using Vortice.Vulkan;

namespace Dwarf.Rendering.Renderer2D.Components;

public class Tilemap : Component, IDrawable2D {
  private readonly Application _application;

  public Vector2I TilemapSize { get; private set; }
  public int TileSize { get; private set; }
  public List<TilemapLayer> Layers { get; init; } = [];
  public List<Sprite> Backgrounds { get; init; } = [];
  public Mesh CollisionMesh => Layers.Where(x => x.IsCollision).First().LayerMesh;

  private VkPipelineLayout _pipelineLayout;

  public Entity Entity => Owner;
  public bool Active => Owner.Active;
  public bool NeedPipelineCache => true;
  public bool DescriptorBuilt => Texture != null && Texture.TextureDescriptor != 0;
  public ITexture Texture => null!;

  public Tilemap() {
    _application = Application.Instance;
    TilemapSize = new Vector2I(0, 0);
  }

  public Tilemap(Application app, Vector2I tileMapSize, int tileSize) {
    _application = app;
    TilemapSize = tileMapSize;
    TileSize = tileSize;
  }

  public Task Bind(nint commandBuffer, uint index) {
    return Task.CompletedTask;
  }

  public Task Draw(nint commandBuffer, uint index, uint firstInstance) {
    DrawLayers(commandBuffer);

    return Task.CompletedTask;
  }

  public void CachePipelineLayout(object pipelineLayout) {
    _pipelineLayout = (VkPipelineLayout)pipelineLayout;
  }

  private void DrawLayers(nint commandBuffer) {
    for (int i = 0; i < Layers.Count; i++) {
      if (Layers[i].LayerMesh.VertexBuffer == null) {
        Logger.Warn($"Vertex Buffer of Layer {i} is null");
        continue;
      }

      Descriptor.BindDescriptorSet(Layers[i].LayerTexture.TextureDescriptor, commandBuffer, _pipelineLayout, 2, 1);

      _application.Renderer.CommandList.BindVertex(commandBuffer, Layers[i].LayerMesh.VertexBuffer!, 0);
      if (Layers[i].LayerMesh!.IndexBuffer != null) {
        _application.Renderer.CommandList.BindIndex(commandBuffer, Layers[i].LayerMesh.IndexBuffer!, 0);
      }

      if (Layers[i].LayerMesh.HasIndexBuffer) {
        _application.Renderer.CommandList.DrawIndexed(commandBuffer, Layers[i].LayerMesh.IndexCount, 1, 0, 0, 0);
      } else {
        _application.Renderer.CommandList.Draw(commandBuffer, Layers[i].LayerMesh.VertexCount, 1, 0, 0);
      }
    }

    // DrawBackgrounds(commandBuffer);
  }

  private void DrawBackgrounds(nint commandBuffer) {
    for (int i = 0; i < Backgrounds.Count; i++) {
      Descriptor.BindDescriptorSet(Backgrounds[i].Texture.TextureDescriptor, commandBuffer, _pipelineLayout, 2, 1);
      Backgrounds[i].Bind(commandBuffer, 0);
      Backgrounds[i].Draw(commandBuffer);
    }
  }

  public void BuildDescriptors(IDescriptorSetLayout descriptorSetLayout, IDescriptorPool descriptorPool) {
    foreach (var layer in Layers) {
      layer.LayerTexture.BuildDescriptor(descriptorSetLayout, descriptorPool);
    }
    // _tilemapAtlas?.BuildDescriptor(descriptorSetLayout, descriptorPool);
  }

  public void CreateTilemap(string[] imageSource) {
    for (int i = 0; i < Layers.Count; i++) {
      Logger.Info($"Is coll: {Layers[i].IsCollision}");
      Layers[i].GenerateMesh();
      // Layers[i].SetupTexture(imageSource[i]);
    }
  }

  public void CreateBackgrounds(BackgroundData[] backgrounds) {
    for (int i = 0; i < backgrounds.Length; i++) {
      // var sprite = new Sprite(_application, src, Sprite.SPRITE_TILE_SIZE_NONE, false);

      // target 9

      var rawCount = (float)backgrounds[i].Width / 100;
      // Logger.Info(rawCount);
      var repeatCount = (int)MathF.Round(rawCount * rawCount);
      var offset = backgrounds[i].PositionOffset / 1000;
      // offset.X -= repeatCount / 2;
      offset.Y -= LocalSizeY / 5;
      offset.X -= LocalSizeX / 20;
      //offset.X += offset.X;

      var bgEntity = new Entity() {
        Name = $"tilemap-bg-{i}"
      };
      bgEntity.AddMaterial();
      bgEntity.AddTransform(new(offset, -10), default, scale: new(1, 1, 1));
      bgEntity.AddSpriteBuilder().AddSprite(backgrounds[i].ImagePath, LocalSizeY / 10, repeatCount).Build();

      // Logger.Info($"Setting offset to {backgrounds[i].PositionOffset}");
      // Logger.Info($"Setting pos to {backgrounds[i].Position}");
      Logger.Info($"Setting repeat count to {repeatCount}");

      _application.AddEntity(bgEntity);
    }
  }

  public void Dispose() {
    foreach (var layer in Layers) {
      layer.Dispose();
    }
    foreach (var sprite in Backgrounds) {
      sprite.Dispose();
    }
    GC.SuppressFinalize(this);
  }

  public Vector2I SpriteSheetSize => new(1, 1);
  public Vector3 WorldSize {
    get {
      var size = Sprite.VERTEX_SIZE * Layers[0].Tiles.GetLength(1);
      return Owner.GetComponent<Transform>().Scale * size;
    }
  }

  public float LocalSizeY => Sprite.VERTEX_SIZE * Layers[0].Tiles.GetLength(1);
  public float LocalSizeX => Sprite.VERTEX_SIZE * Layers[0].Tiles.GetLength(0);

  public int SpriteIndex { get; set; } = 0;
  public bool FlipX { get; set; }
  public bool FlipY { get; set; }

  public int SpriteCount => Layers.Count;
}