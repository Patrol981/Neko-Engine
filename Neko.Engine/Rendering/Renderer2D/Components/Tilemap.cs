using System.Numerics;
using Neko;
using Neko.AbstractionLayer;
using Neko.EntityComponentSystem;
using Neko.Extensions.Logging;
using Neko.Math;
using Neko.Rendering.Renderer2D.Helpers;
using Neko.Rendering.Renderer2D.Interfaces;
using Neko.Rendering.Renderer2D.Models;
using Neko.Vulkan;
using Vortice.Vulkan;

namespace Neko.Rendering.Renderer2D.Components;

public class Tilemap : IDrawable2D {
  private readonly Application _application;

  public Vector2I TilemapSize { get; private set; }
  public int TileSize { get; private set; }
  public List<TilemapLayer> Layers { get; init; } = [];
  public TilemapLayer CollisionLayer { get; set; } = default!;
  public List<Sprite> Backgrounds { get; init; } = [];
  public Mesh CollisionMesh => CollisionLayer.LayerMesh;
  public Mesh Mesh => throw new NotImplementedException();
  public ITexture[] SpriteSheet => [];
  public IDrawable2D[] Children => [.. Layers];

  private VkPipelineLayout _pipelineLayout;

  public Entity Entity { get; init; }
  public bool Active => Entity.Active;
  public ITexture Texture => throw new NotImplementedException();

  public Tilemap(Entity entity) {
    Entity = entity;
    _application = Application.Instance;
    TilemapSize = new Vector2I(0, 0);
  }

  public Tilemap(Entity entity, Application app, Vector2I tileMapSize, int tileSize) {
    Entity = entity;
    _application = app;
    TilemapSize = tileMapSize;
    TileSize = tileSize;
  }

  public Task Bind(nint commandBuffer, uint index) {
    return Task.CompletedTask;
  }

  public Task Draw(nint commandBuffer, uint index, uint firstInstance) {
    // DrawLayers(commandBuffer);

    return Task.CompletedTask;
  }

  public void CachePipelineLayout(object pipelineLayout) {
    _pipelineLayout = (VkPipelineLayout)pipelineLayout;
  }

  // private void DrawLayers(nint commandBuffer) {
  //   for (int i = 0; i < Layers.Count; i++) {
  //     if (Layers[i].LayerMesh.VertexBuffer == null) {
  //       Logger.Warn($"Vertex Buffer of Layer {i} is null");
  //       continue;
  //     }

  //     Descriptor.BindDescriptorSet(Layers[i].LayerTexture.TextureDescriptor, commandBuffer, _pipelineLayout, 2, 1);

  //     _application.Renderer.CommandList.BindVertex(commandBuffer, Layers[i].LayerMesh.VertexBuffer!, 0);
  //     if (Layers[i].LayerMesh!.IndexBuffer != null) {
  //       _application.Renderer.CommandList.BindIndex(commandBuffer, Layers[i].LayerMesh.IndexBuffer!, 0);
  //     }

  //     if (Layers[i].LayerMesh.HasIndexBuffer) {
  //       _application.Renderer.CommandList.DrawIndexed(commandBuffer, Layers[i].LayerMesh.IndexCount, 1, 0, 0, 0);
  //     } else {
  //       _application.Renderer.CommandList.Draw(commandBuffer, Layers[i].LayerMesh.VertexCount, 1, 0, 0);
  //     }
  //   }

  //   // DrawBackgrounds(commandBuffer);
  // }

  // private void DrawBackgrounds(nint commandBuffer) {
  //   for (int i = 0; i < Backgrounds.Count; i++) {
  //     Descriptor.BindDescriptorSet(Backgrounds[i].Texture.TextureDescriptor, commandBuffer, _pipelineLayout, 2, 1);
  //     Backgrounds[i].Bind(commandBuffer, 0);
  //     Backgrounds[i].Draw(commandBuffer);
  //   }
  // }

  public void CreateTilemap(string[] imageSource) {
    for (int i = 0; i < Layers.Count; i++) {
      Logger.Info($"Is coll: {Layers[i].IsCollision}");
      Layers[i].GenerateMesh();
      // Layers[i].SetupTexture(imageSource[i]);
    }

    CollisionLayer.GenerateMesh();
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

      var bgEntity = new Entity($"tilemap-bg-{i}");
      bgEntity.AddTransform(new TransformComponent(new(offset, -10), default, scale: new(1, 1, 1)));
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

  public object Clone() {
    throw new NotImplementedException();
  }

  public Vector2I SpriteSheetSize => new(1, 1);
  public Vector3 WorldSize {
    get {
      var size = Sprite.VERTEX_SIZE * Layers[0].Tiles.GetLength(1);
      return Entity.GetTransform()!.Scale * size;
    }
  }

  public float LocalSizeY => Sprite.VERTEX_SIZE * Layers[0].Tiles.GetLength(1);
  public float LocalSizeX => Sprite.VERTEX_SIZE * Layers[0].Tiles.GetLength(0);

  public int SpriteIndex { get; set; } = 0;
  public bool FlipX { get; set; }
  public bool FlipY { get; set; }

  public int SpriteCount => Layers.Count;
  public float LocalZDepth => 0;
}