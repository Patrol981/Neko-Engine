using System.Numerics;
using System.Runtime.CompilerServices;

using Neko.AbstractionLayer;
using Neko.EntityComponentSystem;
using Neko.Extensions.Logging;
using Neko.Math;
using Vortice.Vulkan;

namespace Neko.Rendering;

public class Mesh : ICloneable {
  private readonly IDevice _device = null!;
  private readonly nint _allocator = IntPtr.Zero;

  // internal Guid EntityId { get; init; }

  public Vertex[] Vertices = [];
  public uint[] Indices = [];

  public ulong VertexCount => (ulong)Vertices.LongLength;
  public ulong IndexCount => (ulong)Indices.LongLength;

  public bool HasIndexBuffer => IndexCount > 0;

  public Guid TextureIdReference = Guid.Empty;
  public Material Material { get; set; } = null!;

  public Matrix4x4 Matrix = Matrix4x4.Identity;

  public BoundingBox BoundingBox;

  public Mesh(nint allocator, IDevice device, Matrix4x4 matrix = default) {
    _allocator = allocator;
    _device = device;
    Matrix = matrix;
  }

  // public Mesh(Guid entitiyId, nint allocator, IDevice device, Matrix4x4 matrix = default) {
  //   EntityId = entitiyId;
  //   _allocator = allocator;
  //   _device = device;
  //   Matrix = matrix;
  // }

  public async void BindToTexture(TextureManager textureManager, string texturePath) {
    TextureIdReference = textureManager.GetTextureIdLocal(texturePath);

    if (TextureIdReference == Guid.Empty) {
      var texture = await TextureLoader.LoadFromPath(_allocator, _device, texturePath);
      textureManager.AddTextureLocal(texture);
      TextureIdReference = textureManager.GetTextureIdLocal(texturePath);

      Logger.Warn($"Could not bind texture to model ({texturePath}) - no such texture in manager");
      Logger.Info($"Binding ({texturePath})");
    }
  }

  public void BindToTexture(TextureManager textureManager, Guid textureId) {
    TextureIdReference = textureId;

    if (TextureIdReference == Guid.Empty) {
      throw new ArgumentException("Guid is empty!");
    }
  }

  public float Height {
    get {
      double minY = double.MaxValue;
      double maxY = double.MinValue;

      foreach (var v in Vertices) {
        if (v.Position.Y < minY)
          minY = v.Position.Y;
        if (v.Position.Y > maxY)
          maxY = v.Position.Y;
      }

      return (float)(maxY - minY);
    }
  }

  public object Clone() {
    var clone = new Mesh(_allocator, _device) {
      Vertices = (Vertex[])Vertices.Clone(),
      Indices = (uint[])Indices.Clone(),

      TextureIdReference = TextureIdReference,
      Material = Material,

      Matrix = Matrix,

      BoundingBox = BoundingBox
    };

    return clone;
  }
}