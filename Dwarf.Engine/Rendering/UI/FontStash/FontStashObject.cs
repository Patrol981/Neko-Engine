using System.Runtime.CompilerServices;

using Dwarf.AbstractionLayer;
using Dwarf.Vulkan;

using FontStashSharp.Interfaces;
using Vortice.Vulkan;

namespace Dwarf.Rendering.UI.FontStash;

public class FontStashObject : IDisposable {
  public class FontMesh {
    public VertexPositionColorTexture[] Vertices = new VertexPositionColorTexture[0];
    public uint[] Indices = new uint[0];
  }

  private const int MAX_SPRITES = 2048;
  private const int MAX_VERTICES = MAX_SPRITES * 4;
  private const int MAX_INDICES = MAX_SPRITES * 6;

  private readonly VulkanDevice _device = null!;
  private readonly nint _allocator = IntPtr.Zero;
  private DwarfBuffer _vertexBuffer = null!;
  private DwarfBuffer _indexBuffer = null!;
  private readonly ulong _vertexCount = 0;
  private ulong _indexCount = 0;

  private readonly FontMesh _fontMesh;

  public FontStashObject(VulkanDevice device) {
    _device = device;

    _fontMesh = new();
    CreateFontMeshIndices();
    CreateIndexBuffer(_fontMesh.Indices);
    CreateVertexBuffer();

    _fontMesh.Vertices = new VertexPositionColorTexture[MAX_VERTICES];
  }

  private void CreateFontMeshIndices() {
    _fontMesh.Indices = new uint[MAX_INDICES];
    for (int i = 0, j = 0; i < MAX_INDICES; i += 6, j += 4) {
      _fontMesh.Indices[i] = (uint)j;
      _fontMesh.Indices[i + 1] = (uint)(j + 1);
      _fontMesh.Indices[i + 2] = (uint)(j + 2);
      _fontMesh.Indices[i + 3] = (uint)(j + 3);
      _fontMesh.Indices[i + 4] = (uint)(j + 2);
      _fontMesh.Indices[i + 5] = (uint)(j + 1);
    }
  }

  public void CreateFontMeshVertices() {

  }

  public void SetVertexData(int startIndex, int vertexCount) {
    WriteToBuffer(startIndex, vertexCount);
  }

  private void WriteToBuffer(int startIndex, int vertexCount) {

  }

  private void CopyToBuffer(VertexPositionColorTexture[] vertices) {
    ulong bufferSize = (ulong)Unsafe.SizeOf<VertexPositionColorTexture>() * MAX_VERTICES;
    ulong vertexSize = (ulong)Unsafe.SizeOf<VertexPositionColorTexture>();

    var stagingBuffer = new DwarfBuffer(
      _allocator,
      _device,
      vertexSize,
      _vertexCount,
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent
    );

    stagingBuffer.Map(bufferSize);
    unsafe {
      fixed (VertexPositionColorTexture* vertexColorTexturePtr = vertices) {
        stagingBuffer.WriteToBuffer((nint)vertexColorTexturePtr, bufferSize);
      }
    }
    // stagingBuffer.WriteToBuffer(MemoryUtils.ToIntPtr(vertices), bufferSize);

    _device.CopyBuffer(stagingBuffer.GetBuffer(), _vertexBuffer.GetBuffer(), bufferSize);
    stagingBuffer.Dispose();
  }

  private void CreateVertexBuffer() {
    ulong bufferSize = (ulong)Unsafe.SizeOf<VertexPositionColorTexture>() * MAX_VERTICES;
    ulong vertexSize = (ulong)Unsafe.SizeOf<VertexPositionColorTexture>();

    _vertexBuffer = new DwarfBuffer(
      _allocator,
      _device,
      vertexSize,
      _vertexCount,
      BufferUsage.VertexBuffer | BufferUsage.TransferDst,
      MemoryProperty.DeviceLocal
    );
  }

  private void CreateIndexBuffer(uint[] indices) {
    _indexCount = (ulong)indices.Length;
    ulong bufferSize = sizeof(uint) * _indexCount;
    ulong indexSize = sizeof(uint);

    var stagingBuffer = new DwarfBuffer(
      _allocator,
      _device,
      indexSize,
      _indexCount,
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent
    );

    stagingBuffer.Map(bufferSize);
    unsafe {
      fixed (uint* indicesPtr = indices) {
        stagingBuffer.WriteToBuffer((nint)indicesPtr, bufferSize);
      }
    }
    // stagingBuffer.WriteToBuffer(MemoryUtils.ToIntPtr(indices), bufferSize);

    _indexBuffer = new DwarfBuffer(
      _allocator,
      _device,
      indexSize,
      _indexCount,
      BufferUsage.IndexBuffer | BufferUsage.TransferDst,
      MemoryProperty.DeviceLocal
    );

    _device.CopyBuffer(stagingBuffer.GetBuffer(), _indexBuffer.GetBuffer(), bufferSize);
    stagingBuffer.Dispose();
  }

  public void Dispose() {
    _vertexBuffer.Dispose();
    _indexBuffer.Dispose();
  }

  public FontMesh GetFontMesh() => _fontMesh;
}
