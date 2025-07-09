using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using Dwarf.AbstractionLayer;
using Dwarf.Extensions.Logging;
using Dwarf.Rendering.Renderer2D.Interfaces;
using Dwarf.Rendering.Renderer2D.Models;
using Dwarf.Vulkan;
using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Rendering.Renderer2D;

public class Render2DSystem : SystemBase {
  // private unsafe SpritePushConstant* _spritePushConstant;

  private DwarfBuffer? _globalVertexBuffer;
  private DwarfBuffer? _globalIndexBuffer;
  private DwarfBuffer? _indirectBuffer;

  private List<VkDrawIndexedIndirectCommand> _indirectDrawCommands = [];

  public int LastKnownElemCount { get; private set; } = 0;

  public Render2DSystem(
    nint allocator,
    IDevice device,
    IRenderer renderer,
    TextureManager textureManager,
    Dictionary<string, IDescriptorSetLayout> externalLayouts,
    IPipelineConfigInfo configInfo = null!
  ) : base(allocator, device, renderer, textureManager, configInfo) {
    IDescriptorSetLayout[] descriptorSetLayouts = [
      externalLayouts["Global"],
      externalLayouts["SpriteData"],
      _textureManager.AllTexturesSetLayout,
    ];

    AddPipelineData(new() {
      RenderPass = renderer.GetSwapchainRenderPass(),
      VertexName = "sprite_vertex",
      FragmentName = "sprite_fragment",
      PipelineProvider = new PipelineSpriteProvider(),
      DescriptorSetLayouts = descriptorSetLayouts,
    });

    _descriptorPool = new VulkanDescriptorPool.Builder(_device)
      .SetMaxSets(CommonConstants.MAX_SETS)
      .AddPoolSize(DescriptorType.UniformBuffer, CommonConstants.MAX_SETS)
      .AddPoolSize(DescriptorType.SampledImage, CommonConstants.MAX_SETS)
      .AddPoolSize(DescriptorType.Sampler, CommonConstants.MAX_SETS)
      .AddPoolSize(DescriptorType.InputAttachment, CommonConstants.MAX_SETS)
      .AddPoolSize(DescriptorType.StorageBuffer, CommonConstants.MAX_SETS)
      .SetPoolFlags(DescriptorPoolCreateFlags.UpdateAfterBind)
      .Build();

    _texturesCount = -1;
  }

  public unsafe void Setup(ReadOnlySpan<IDrawable2D> drawables, ref TextureManager textures) {
    if (drawables.Length < 1) {
      Logger.Warn("Entities that are capable of using 2D renderer are less than 1, thus 2D Render System won't be recreated");
      return;
    }

    Logger.Info($"Recreating Renderer 2D [{_texturesCount}]");

    _texturesCount = CalculateTextureCount(drawables);
    LastKnownElemCount = CalculateLastKnownElemCount(drawables);

    CreateVertexBuffer(drawables);
    CreateIndexBuffer(drawables);

    CreateIndirectCommands(drawables);
    CreateIndirectBuffer();
  }

  public bool CheckSizes(ReadOnlySpan<IDrawable2D> drawables) {
    // if (_texturesCount == -1) {
    //   var textureManager = Application.Instance.TextureManager;
    //   // Setup(drawables, ref textureManager);
    // }
    var newCount = CalculateLastKnownElemCount(drawables);
    if (newCount != LastKnownElemCount) {
      LastKnownElemCount = newCount;
      CreateIndirectCommands(drawables);
      CreateIndirectBuffer();
    }

    if (newCount > LastKnownElemCount) {
      return false;
    }

    return true;
  }
  private static int CalculateTextureCount(ReadOnlySpan<IDrawable2D> drawables) {
    int count = 0;
    for (int i = 0; i < drawables.Length; i++) {
      count += drawables[i].SpriteCount;
    }
    return count;
  }

  private static int CalculateLastKnownElemCount(ReadOnlySpan<IDrawable2D> drawables) {
    // int count = 0;
    // for (int i = 0; i < drawables.Length; i++) {
    //   if (drawables[i].HasMultipleMeshes) {
    //     count += drawables[i].Meshes.Length;
    //   } else {
    //     count++;
    //   }
    // }

    return drawables.Length;
  }

  public static void Update(ReadOnlySpan<IDrawable2D> drawables, out SpritePushConstant140[] spriteData) {
    spriteData = new SpritePushConstant140[drawables.Length];

    for (int i = 0; i < drawables.Length; i++) {
      var target = drawables[i];

      var myTexId = GetIndexOfMyTexture(target.Texture.TextureName);
      if (!myTexId.HasValue) throw new ArgumentException("", paramName: myTexId.ToString());

      spriteData[i] = new() {
        SpriteMatrix = target.Entity.TryGetComponent<Transform>()?.Matrix4 ?? Matrix4x4.Identity,
        SpriteSheetData = new(target.SpriteSheetSize.X, target.SpriteSheetSize.Y, target.SpriteIndex, target.FlipX ? 1.0f : 0.0f),
        SpriteSheetData2 = new(target.FlipY ? 1.0f : 0.0f, myTexId.Value, -1, -1)
      };
    }
  }

  public unsafe void Render(FrameInfo frameInfo, ReadOnlySpan<IDrawable2D> drawables) {
    if (_globalIndexBuffer is null || _globalVertexBuffer is null || _indirectBuffer is null) return;
    BindPipeline(frameInfo.CommandBuffer);

    Descriptor.BindDescriptorSet(frameInfo.GlobalDescriptorSet, frameInfo, PipelineLayout, 0, 1);
    Descriptor.BindDescriptorSet(_textureManager.AllTexturesDescriptor, frameInfo, PipelineLayout, 2, 1);
    Descriptor.BindDescriptorSet(frameInfo.SpriteDataDescriptorSet, frameInfo, PipelineLayout, 1, 1);
    _renderer.CommandList.BindVertex(frameInfo.CommandBuffer, _globalVertexBuffer, 0);
    _renderer.CommandList.BindIndex(frameInfo.CommandBuffer, _globalIndexBuffer, 0);
    _renderer.CommandList.DrawIndexedIndirect(
      frameInfo.CommandBuffer,
      _indirectBuffer.GetBuffer(),
      0,
      (uint)_indirectDrawCommands.Count,
      (uint)Unsafe.SizeOf<VkDrawIndexedIndirectCommand>()
    );
  }

  public unsafe void Render_(FrameInfo frameInfo, ReadOnlySpan<IDrawable2D> drawables) {
    if (_globalIndexBuffer == null ||
        _globalVertexBuffer == null ||
        _textureManager.AllTexturesDescriptor == 0)
      return;

    BindPipeline(frameInfo.CommandBuffer);
    Descriptor.BindDescriptorSet(frameInfo.GlobalDescriptorSet, frameInfo, PipelineLayout, 0, 1);
    Descriptor.BindDescriptorSet(_textureManager.AllTexturesDescriptor, frameInfo, PipelineLayout, 2, 1);
    Descriptor.BindDescriptorSet(frameInfo.SpriteDataDescriptorSet, frameInfo, PipelineLayout, 1, 1);
    _renderer.CommandList.BindVertex(frameInfo.CommandBuffer, _globalVertexBuffer, 0);
    _renderer.CommandList.BindIndex(frameInfo.CommandBuffer, _globalIndexBuffer, 0);

    uint indexOffset = 0;

    for (uint instance = 0; instance < (uint)drawables.Length; instance++) {
      var d = drawables[(int)instance];
      var mesh = d.Mesh;

      uint thisCount = (uint)mesh.Indices.Length;

      if (!d.Active || d.Entity.CanBeDisposed || mesh.IndexBuffer == null) {
        indexOffset += thisCount;
        continue;
      }

      _renderer.CommandList.DrawIndexed(
        frameInfo.CommandBuffer,
        indexCount: thisCount,
        instanceCount: 1,
        firstIndex: indexOffset,
        vertexOffset: 0,
        firstInstance: instance
      );

      indexOffset += thisCount;
    }
  }

  private static int? GetIndexOfMyTexture(string texName) {
    return Application.Instance.TextureManager.PerSceneLoadedTextures.Where(x => x.Value.TextureName == texName).FirstOrDefault().Value.TextureManagerIndex;
  }

  private void CreateIndirectCommands(ReadOnlySpan<IDrawable2D> drawables) {
    _indirectDrawCommands.Clear();
    uint indexOffset = 0;

    for (int i = 0; i < drawables.Length; i++) {
      var mesh = drawables[i].Mesh;
      if (mesh.IndexBuffer == null)
        continue;

      var cmd = new VkDrawIndexedIndirectCommand {
        indexCount = (uint)mesh.Indices.Length,
        instanceCount = 1,
        firstIndex = indexOffset,
        vertexOffset = 0,
        firstInstance = (uint)i
      };

      _indirectDrawCommands.Add(cmd);
      indexOffset += (uint)mesh.Indices.Length;
    }
  }

  private unsafe void CreateIndirectBuffer() {
    _indirectBuffer?.Dispose();

    var size = _indirectDrawCommands.Count * Unsafe.SizeOf<VkDrawIndexedIndirectCommand>();

    var stagingBuffer = new DwarfBuffer(
      _allocator,
      _device,
      (ulong)size,
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
      stagingBuffer: true,
      cpuAccessible: true
    );

    stagingBuffer.Map((ulong)size);
    fixed (VkDrawIndexedIndirectCommand* pIndirectCommands = _indirectDrawCommands.ToArray()) {
      stagingBuffer.WriteToBuffer((nint)pIndirectCommands, (ulong)size);
    }

    _indirectBuffer = new DwarfBuffer(
      _allocator,
      _device,
      (ulong)size,
      BufferUsage.TransferDst | BufferUsage.IndirectBuffer,
      MemoryProperty.DeviceLocal
    );

    _device.CopyBuffer(stagingBuffer.GetBuffer(), _indirectBuffer.GetBuffer(), (ulong)size);
    stagingBuffer.Dispose();
  }

  private unsafe void CreateIndexBuffer(ReadOnlySpan<IDrawable2D> drawables) {
    _globalIndexBuffer?.Dispose();

    var adjustedIndices = new List<uint>();
    uint vertexOffset = 0;

    for (int i = 0; i < drawables.Length; i++) {
      var mesh = drawables[i].Mesh;
      if (mesh.IndexBuffer == null) continue;

      foreach (var idx in mesh.Indices) {
        adjustedIndices.Add(idx + vertexOffset);
      }

      vertexOffset += (uint)mesh.Vertices.Length;
    }

    var indexByteSize = (ulong)adjustedIndices.Count * sizeof(uint);

    var stagingBuffer = new DwarfBuffer(
      _allocator,
      _device,
      indexByteSize,
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
      stagingBuffer: true,
      cpuAccessible: true
    );

    stagingBuffer.Map(indexByteSize);
    fixed (uint* pSrc = adjustedIndices.ToArray()) {
      stagingBuffer.WriteToBuffer((nint)pSrc, indexByteSize);
    }

    _globalIndexBuffer = new DwarfBuffer(
      _allocator,
      _device,
      (ulong)Unsafe.SizeOf<uint>(),
      (uint)adjustedIndices.Count,
      BufferUsage.IndexBuffer | BufferUsage.TransferDst,
      MemoryProperty.DeviceLocal
    );

    _device.CopyBuffer(
      stagingBuffer.GetBuffer(),
      _globalIndexBuffer.GetBuffer(),
      indexByteSize
    );
    stagingBuffer.Dispose();
  }

  private unsafe void CreateIndexBuffer_(ReadOnlySpan<IDrawable2D> drawables) {
    _globalIndexBuffer?.Dispose();
    ulong size = 0;
    List<uint> indices = [];
    for (int i = 0; i < drawables.Length; i++) {
      var buffer = drawables[i].Mesh.IndexBuffer;
      if (buffer != null) {
        size += buffer.GetBufferSize();
        indices.AddRange(drawables[i].Mesh.Indices);
      }
    }

    var stagingBuffer = new DwarfBuffer(
      _allocator,
      _device,
      size,
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
      stagingBuffer: true,
      cpuAccessible: true
    );

    stagingBuffer.Map(size);
    fixed (uint* pIndices = indices.ToArray()) {
      stagingBuffer.WriteToBuffer((nint)pIndices, size);
    }

    _globalIndexBuffer = new DwarfBuffer(
      _allocator,
      _device,
      size,
      (ulong)indices.Count,
      BufferUsage.IndexBuffer | BufferUsage.TransferDst,
      MemoryProperty.DeviceLocal
    );

    _device.CopyBuffer(stagingBuffer.GetBuffer(), _globalIndexBuffer.GetBuffer(), size);
    stagingBuffer.Dispose();
  }

  private unsafe void CreateVertexBuffer(ReadOnlySpan<IDrawable2D> drawables) {
    _globalVertexBuffer?.Dispose();
    ulong size = 0;
    List<Vertex> vertices = [];

    for (int i = 0; i < drawables.Length; i++) {
      var buffer = drawables[i].Mesh.VertexBuffer;
      if (buffer != null) {
        size += buffer.GetBufferSize();
        vertices.AddRange(drawables[i].Mesh.Vertices);
      }
    }

    var stagingBuffer = new DwarfBuffer(
      _allocator,
      _device,
      size,
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
      stagingBuffer: true,
      cpuAccessible: true
    );

    stagingBuffer.Map(size);
    fixed (Vertex* pVertices = vertices.ToArray()) {
      stagingBuffer.WriteToBuffer((nint)pVertices, size);
    }

    _globalVertexBuffer = new DwarfBuffer(
      _allocator,
      _device,
      size,
      (ulong)vertices.Count,
      BufferUsage.VertexBuffer | BufferUsage.TransferDst,
      MemoryProperty.DeviceLocal
    );

    _device.CopyBuffer(stagingBuffer.GetBuffer(), _globalVertexBuffer.GetBuffer(), size);
    stagingBuffer.Dispose();
  }

  public override unsafe void Dispose() {
    _device.WaitQueue();
    _globalVertexBuffer?.Dispose();
    _globalIndexBuffer?.Dispose();
    _indirectBuffer?.Dispose();
    base.Dispose();
  }
}
