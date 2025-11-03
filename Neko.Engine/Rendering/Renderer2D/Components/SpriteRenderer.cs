using System.Numerics;
using Neko.AbstractionLayer;
using Neko.EntityComponentSystem;
using Neko.Math;
using Neko.Rendering.Renderer2D.Interfaces;
using Neko.Rendering.Renderer2D.Models;

namespace Neko.Rendering.Renderer2D.Components;

public class SpriteRenderer : IDrawable2D {
  public delegate void OnAnimationEnd();

  public Sprite[] Sprites { get; init; } = [];
  public int CurrentSprite { get; private set; } = 0;
  private Vector3 _lastKnownScale = Vector3.Zero;
  private Vector2 _cachedSize = Vector2.Zero;
  private Bounds2D _cachedBounds = Bounds2D.Zero;
  public Vector2 Size => GetSize();
  public Bounds2D Bounds => GetBounds();
  public Mesh CollisionMesh => Sprites[CurrentSprite].SpriteMesh;
  public Mesh Mesh => Sprites[CurrentSprite].SpriteMesh;
  public IDrawable2D[] Children => [];

  public Entity Entity { get; private set; }
  public bool Active => Entity.Active;
  public int SpriteIndex => Sprites[CurrentSprite].SpriteIndex;
  public int SpriteCount => Sprites.Length;
  public ITexture Texture => Sprites[CurrentSprite].Texture;
  public ITexture[] SpriteSheet => [.. Sprites.Select(x => x.Texture)];
  public Vector2I SpriteSheetSize => Sprites[CurrentSprite].SpriteSheetSize;
  public bool FlipX { get; set; }
  public bool FlipY { get; set; }

  public float DirectionX => FlipX ? -1 : 1;

  public float LocalZDepth => 0;

  private ShaderInfo _customShader = new();
  public ShaderInfo CustomShader => _customShader;

  public SpriteRenderer() { }

  public void Next(OnAnimationEnd onAnimationEnd) {
    if (Sprites[CurrentSprite].SpriteIndex > Sprites[CurrentSprite].MaxIndex) {
      Sprites[CurrentSprite].SpriteIndex = 0;
      onAnimationEnd.Invoke();
    }
    Sprites[CurrentSprite].SpriteIndex += 1;
  }

  public void NextSprite() {
    ResetSprite(CurrentSprite);
    CurrentSprite += 1;
    if (CurrentSprite > SpriteCount - 1) {
      CurrentSprite = 0;
    }
  }

  public void SetSpriteSheet(string spriteLike) {
    ResetSprite(CurrentSprite);
    var target = Sprites
      .Select((x, index) => (x, index))
      .Where(item => item.x.Texture.TextureName.Contains(spriteLike))
      .FirstOrDefault();
    if (target.x != null) {
      CurrentSprite = target.index;
    } else {
      CurrentSprite = 0;
    }
  }

  public void SetSpriteSheet(int index) {
    CurrentSprite = index;
  }

  public void BuildDescriptors(IDescriptorSetLayout descriptorSetLayout, IDescriptorPool descriptorPool) {
    foreach (var sprite in Sprites) {
      sprite.BuildDescriptors(descriptorSetLayout, descriptorPool);
    }
  }

  public Task Bind(nint commandBuffer, uint index) {
    // Sprites[CurrentSprite].Bind(commandBuffer, index);
    return Task.CompletedTask;
  }

  public Task Draw(nint commandBuffer, uint index = 0, uint firstInstance = 0) {
    // Sprites[CurrentSprite].Draw(commandBuffer, index, firstInstance);
    return Task.CompletedTask;
  }

  public void CachePipelineLayout(object pipelineLayout) {
    throw new InvalidOperationException("This component does not need caching");
  }

  public void Dispose() {
    foreach (var sprite in Sprites) {
      sprite.Dispose();
    }
    GC.SuppressFinalize(this);
  }

  public object Clone() {
    var sr = new SpriteRenderer() {
      Sprites = [.. Sprites.Select(static x => {
        var clone = (Sprite)x.Clone();
        return clone;
       })],
      CurrentSprite = CurrentSprite,
      FlipX = FlipX,
      FlipY = FlipY
    };

    return sr;
  }

  private void ResetSprite(int index) {
    Sprites[index].Reset();
  }

  private Bounds2D GetBounds() {
    var pos = Entity.GetTransform()!.Position;
    var size = GetSize();

    _cachedBounds = new() {
      Min = new Vector2(pos.X, pos.Y),
      Max = new Vector2(pos.X + size.X, pos.Y + size.Y)
    };

    return _cachedBounds;
  }

  private Vector2 GetSize() {
    var scale = Entity.GetTransform()!.Scale;
    if (_lastKnownScale == scale) return _cachedSize;

    float minX, minY, maxX, maxY;

    maxX = Sprites[CurrentSprite].SpriteMesh.Vertices[0].Position.X;
    maxY = Sprites[CurrentSprite].SpriteMesh.Vertices[0].Position.Y;
    minX = Sprites[CurrentSprite].SpriteMesh.Vertices[0].Position.X;
    minY = Sprites[CurrentSprite].SpriteMesh.Vertices[0].Position.Y;

    for (int i = 0; i < Sprites[CurrentSprite].SpriteMesh.Vertices.Length; i++) {
      if (minX > Sprites[CurrentSprite].SpriteMesh.Vertices[i].Position.X)
        minX = Sprites[CurrentSprite].SpriteMesh.Vertices[i].Position.X;
      if (maxX < Sprites[CurrentSprite].SpriteMesh.Vertices[i].Position.X)
        maxX = Sprites[CurrentSprite].SpriteMesh.Vertices[i].Position.X;

      if (minY > Sprites[CurrentSprite].SpriteMesh.Vertices[i].Position.Y)
        minY = Sprites[CurrentSprite].SpriteMesh.Vertices[i].Position.Y;
      if (maxY < Sprites[CurrentSprite].SpriteMesh.Vertices[i].Position.Y)
        maxY = Sprites[CurrentSprite].SpriteMesh.Vertices[i].Position.Y;
    }

    _lastKnownScale = scale;


    _cachedSize = new Vector2(
      MathF.Abs(minX - maxX) * scale.X,
      MathF.Abs(minY - maxY) * scale.Y
    );

    return _cachedSize;
  }

  public void SetCustomShader(ShaderInfo shaderInfo) {
    _customShader = shaderInfo;
  }

  public void SetShaderTextureInfo(Guid textureId) {
    _customShader.ShaderTextureId = textureId;
  }

  public class Builder {
    private readonly Application _app;
    private readonly List<Sprite> _sprites = [];
    private Entity? _entity;

    public Builder(Application app, Entity? entity) {
      _app = app;
      _entity = entity;

      Application.Mutex.WaitOne();
    }

    public Builder AddSpriteSheet(string spriteTexture, int rows, int columns) {
      _sprites.Add(new Sprite(_app, spriteTexture, rows, columns, true));
      return this;
    }

    public Builder AddSprite(string spriteTexture) {
      _sprites.Add(new Sprite(_app, spriteTexture, Sprite.SPRITE_TILE_SIZE_NONE, false));
      return this;
    }

    public Builder AddSprite(string spriteTexture, float vertexSize, int repeatCount) {
      _sprites.Add(new Sprite(_app, spriteTexture, vertexSize, repeatCount, 1));
      return this;
    }

    // public Builder AddSprite(ITexture spriteTexture) {

    //   return this;
    // }

    // public Builder AddSprite(ITexture spriteTexture, float vertexSize, int repeatCount) {
    //   return this;
    // }

    public SpriteRenderer? Build() {
      var spriteRenderer = new SpriteRenderer() {
        Sprites = [.. _sprites],
        Entity = _entity ?? null!
      };
      if (_entity != null) {
        _entity.AddDrawable2D(spriteRenderer);
        Application.Mutex.ReleaseMutex();
        return null!;
      } else {
        Application.Mutex.ReleaseMutex();
        return spriteRenderer;
      }
    }
  }
}