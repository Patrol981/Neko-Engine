using System.Numerics;
using System.Runtime.CompilerServices;

using Dwarf.AbstractionLayer;
using Dwarf.Extensions.Logging;
using Dwarf.Math;
using Vortice.Vulkan;

namespace Dwarf.Rendering;

public class Mesh : IDisposable, ICloneable {
  private readonly IDevice _device = null!;
  private readonly nint _allocator = IntPtr.Zero;

  public Vertex[] Vertices = [];
  public uint[] Indices = [];

  public DwarfBuffer? VertexBuffer { get; private set; }
  public DwarfBuffer? IndexBuffer { get; private set; }

  public ulong VertexCount = 0;
  public ulong IndexCount = 0;

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

  public unsafe Task CreateVertexBuffer(ulong size = 0) {
    ulong bufferSize;
    ulong vertexSize;
    VertexCount = (ulong)Vertices.Length;

    if (size > 0) {
      bufferSize = size * VertexCount;
      vertexSize = size;
    } else {
      bufferSize = ((ulong)Unsafe.SizeOf<Vertex>()) * VertexCount;
      vertexSize = (ulong)Unsafe.SizeOf<Vertex>();
    }

    var stagingBuffer = new DwarfBuffer(
      _allocator,
      _device,
      vertexSize,
      VertexCount,
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
      default,
      true
    );

    stagingBuffer.Map(bufferSize);
    fixed (Vertex* verticesPtr = Vertices) {
      stagingBuffer.WriteToBuffer((nint)verticesPtr, bufferSize);
    }

    VertexBuffer = new DwarfBuffer(
      _allocator,
      _device,
      vertexSize,
      VertexCount,
      BufferUsage.VertexBuffer | BufferUsage.TransferDst,
      MemoryProperty.DeviceLocal
    );

    _device.CopyBuffer(stagingBuffer.GetBuffer(), VertexBuffer.GetBuffer(), bufferSize);
    stagingBuffer.Dispose();
    return Task.CompletedTask;
  }

  public unsafe Task CreateIndexBuffer() {
    IndexCount = (ulong)Indices.Length;
    if (!HasIndexBuffer) return Task.CompletedTask;

    ulong bufferSize = sizeof(uint) * IndexCount;
    ulong indexSize = sizeof(uint);

    var stagingBuffer = new DwarfBuffer(
      _allocator,
      _device,
      indexSize,
      IndexCount,
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
      default,
      true
    );

    stagingBuffer.Map(bufferSize);
    fixed (uint* indicesPtr = Indices) {
      stagingBuffer.WriteToBuffer((nint)indicesPtr, bufferSize);
    }

    IndexBuffer = new DwarfBuffer(
      _allocator,
      _device,
      indexSize,
      IndexCount,
      BufferUsage.IndexBuffer | BufferUsage.TransferDst,
      MemoryProperty.DeviceLocal
    );

    _device.CopyBuffer(stagingBuffer.GetBuffer(), IndexBuffer.GetBuffer(), bufferSize);
    stagingBuffer.Dispose();
    return Task.CompletedTask;
  }

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

  public void Dispose() {
    VertexBuffer?.Dispose();
    if (HasIndexBuffer) {
      IndexBuffer?.Dispose();
    }
  }

  public object Clone() {
    var clone = new Mesh(_allocator, _device) {
      Vertices = Vertices,
      Indices = Indices,

      VertexCount = VertexCount,
      IndexCount = IndexCount,

      TextureIdReference = TextureIdReference,
      Material = Material,

      Matrix = Matrix,

      BoundingBox = BoundingBox
    };

    return clone;

    // public DwarfBuffer? VertexBuffer { get; private set; }
    // public DwarfBuffer? IndexBuffer { get; private set; }
  }
}