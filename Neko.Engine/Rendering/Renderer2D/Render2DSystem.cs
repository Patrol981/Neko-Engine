using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using Neko.AbstractionLayer;
using Neko.EntityComponentSystem;
using Neko.Extensions.Logging;
using Neko.Rendering.Renderer2D.Interfaces;
using Neko.Rendering.Renderer2D.Models;
using Neko.Vulkan;
using Vortice.Vulkan;

namespace Neko.Rendering.Renderer2D;

public class Render2DSystem : SystemBase {
  private NekoBuffer? _globalVertexBuffer;
  private NekoBuffer? _globalIndexBuffer;
  private NekoBuffer[] _indirectBuffers = [];
  private Dictionary<uint, IndirectData> _indirectData = [];
  private List<VkDrawIndexedIndirectCommand> _indirectDrawCommands = [];
  private IDrawable2D[] _drawableCache = [];
  private uint _instanceIndex = 0;

  private BufferPool _bufferPool = null!;
  private List<VertexBinding> _vertexBindings = [];

  public int LastKnownElemCount { get; private set; } = 0;

  public Render2DSystem(
    Application app,
    nint allocator,
    VulkanDevice device,
    IRenderer renderer,
    TextureManager textureManager,
    Dictionary<string, IDescriptorSetLayout> externalLayouts,
    IPipelineConfigInfo configInfo = null!
  ) : base(app, allocator, device, renderer, textureManager, configInfo) {
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
    LastKnownElemCount = CalculateTextureCount(drawables);
    _instanceIndex = 0;

    CreateVertexBuffer(drawables);
    CreateIndexBuffer(drawables);

    CreateIndirectCommands(drawables);
    CreateIndirectBuffer(ref _indirectData, ref _indirectBuffers);
  }

  public bool CheckSizes(ReadOnlySpan<IDrawable2D> drawables) {
    // if (_texturesCount == -1) {
    //   var textureManager = Application.Instance.TextureManager;
    //   // Setup(drawables, ref textureManager);
    // }
    var newCount = CalculateTextureCount(drawables);
    if (newCount != LastKnownElemCount) {
      LastKnownElemCount = newCount;
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
    int count = 0;
    for (int i = 0; i < drawables.Length; i++) {
      if (drawables[i].Children.Length > 0) {
        count += drawables[i].Children.Length;
      } else {
        count++;
      }
    }

    return count;

    // return drawables.Length;
  }

  public void Update(ReadOnlySpan<IDrawable2D> drawables, out SpritePushConstant140[] spriteData) {
    spriteData = new SpritePushConstant140[_drawableCache.Length];

    for (int i = 0; i < _drawableCache.Length; i++) {
      var target = _drawableCache[i];

      var myTexId = GetIndexOfMyTexture(target.Texture.TextureName);
      if (!myTexId.HasValue) throw new ArgumentException("", paramName: myTexId.ToString());

      if (target.LocalZDepth != 0) {
        spriteData[i] = new() {
          SpriteMatrix = target.Entity.GetTransform()?.Matrix().OverrideZDepth(target.LocalZDepth) ?? Matrix4x4.Identity,
          SpriteSheetData = new(target.SpriteSheetSize.X, target.SpriteSheetSize.Y, target.SpriteIndex, target.FlipX ? 1.0f : 0.0f),
          SpriteSheetData2 = new(target.FlipY ? 1.0f : 0.0f, myTexId.Value, -1, -1)
        };
      } else {
        spriteData[i] = new() {
          SpriteMatrix = target.Entity.GetTransform()?.Matrix() ?? Matrix4x4.Identity,
          SpriteSheetData = new(target.SpriteSheetSize.X, target.SpriteSheetSize.Y, target.SpriteIndex, target.FlipX ? 1.0f : 0.0f),
          SpriteSheetData2 = new(target.FlipY ? 1.0f : 0.0f, myTexId.Value, -1, -1)
        };
      }
    }
  }

  public unsafe void Render(FrameInfo frameInfo, ReadOnlySpan<IDrawable2D> drawables) {
    if (_globalIndexBuffer == null) return;

    BindPipeline(frameInfo.CommandBuffer);

    Descriptor.BindDescriptorSet(_device, frameInfo.GlobalDescriptorSet, frameInfo, PipelineLayout, 0, 1);
    Descriptor.BindDescriptorSet(_device, _textureManager.AllTexturesDescriptor, frameInfo, PipelineLayout, 2, 1);
    Descriptor.BindDescriptorSet(_device, frameInfo.SpriteDataDescriptorSet, frameInfo, PipelineLayout, 1, 1);

    _renderer.CommandList.BindIndex(frameInfo.CommandBuffer, _globalIndexBuffer, 0);

    foreach (var container in _indirectData) {
      var targetVertex = _bufferPool.GetVertexBuffer(container.Key);
      _renderer.CommandList.BindVertex(frameInfo.CommandBuffer, targetVertex, 0);

      _renderer.CommandList.DrawIndexedIndirect(
        frameInfo.CommandBuffer,
        _indirectBuffers[container.Key].GetBuffer(),
        0,
        (uint)container.Value.Commands.Count,
        (uint)Unsafe.SizeOf<VkDrawIndexedIndirectCommand>()
      );
    }
  }

  public unsafe void Render_(FrameInfo frameInfo, ReadOnlySpan<IDrawable2D> drawables) {
    BindPipeline(frameInfo.CommandBuffer);
    Descriptor.BindDescriptorSet(_device, frameInfo.GlobalDescriptorSet, frameInfo, PipelineLayout, 0, 1);
    Descriptor.BindDescriptorSet(_device, _textureManager.AllTexturesDescriptor, frameInfo, PipelineLayout, 2, 1);
    Descriptor.BindDescriptorSet(_device, frameInfo.SpriteDataDescriptorSet, frameInfo, PipelineLayout, 1, 1);
    // _renderer.CommandList.BindVertex(frameInfo.CommandBuffer, _globalVertexBuffer, 0);
    _renderer.CommandList.BindIndex(frameInfo.CommandBuffer, _globalIndexBuffer!, 0);

    uint indexOffset = 0;

    for (uint instance = 0; instance < (uint)_drawableCache.Length; instance++) {
      var d = _drawableCache[(int)instance];
      var mesh = d.Mesh;

      var bindInfo = _vertexBindings[(int)instance];
      var buffer = _bufferPool.GetVertexBuffer(bindInfo.BufferIndex);
      _renderer.CommandList.BindVertex(frameInfo.CommandBuffer, buffer, 0);

      uint thisCount = (uint)mesh.Indices.Length;

      if (!d.Active || d.Entity.CanBeDisposed || mesh.IndexCount < 1) {
        indexOffset += thisCount;
        continue;
      }

      _renderer.CommandList.DrawIndexed(
        frameInfo.CommandBuffer,
        indexCount: thisCount,
        instanceCount: 1,
        firstIndex: bindInfo.FirstIndexOffset,
        vertexOffset: (int)bindInfo.FirstVertexOffset,
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
      if (mesh.IndexCount < 1)
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

  private unsafe void CreateIndirectBuffer(ref Dictionary<uint, IndirectData> pair, ref NekoBuffer[] buffArray) {
    foreach (var buff in buffArray) {
      buff?.Dispose();
    }
    Array.Clear(buffArray);
    buffArray = new NekoBuffer[pair.Keys.Count];
    int i = 0;

    foreach (var commands in pair) {
      var size = commands.Value.Commands.Count * Unsafe.SizeOf<VkDrawIndexedIndirectCommand>();

      var stagingBuffer = new NekoBuffer(
        _allocator,
        _device,
        (ulong)size,
        BufferUsage.TransferSrc,
        MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
        stagingBuffer: true,
        cpuAccessible: true
      );

      stagingBuffer.Map((ulong)size);
      fixed (VkDrawIndexedIndirectCommand* pIndirectCommands = commands.Value.Commands.ToArray()) {
        stagingBuffer.WriteToBuffer((nint)pIndirectCommands, (ulong)size);
      }

      var inBuff = new NekoBuffer(
        _allocator,
        _device,
        (ulong)size,
        BufferUsage.TransferDst | BufferUsage.IndirectBuffer,
        MemoryProperty.DeviceLocal
      );

      _device.CopyBuffer(stagingBuffer.GetBuffer(), inBuff.GetBuffer(), (ulong)size);
      stagingBuffer.Dispose();

      buffArray[i] = inBuff;
      i++;
    }
  }

  private void AddIndirectCommand(
    uint index,
    IDrawable2D drawable,
    VertexBinding vertexBinding,
    ref Dictionary<uint, IndirectData> pair,
    ref uint instanceIndex,
    in uint additionalIndexOffset = 0
  ) {
    if (!pair.ContainsKey(index)) {
      var id = pair.Keys.Count;
      pair.Add((uint)id, new());
    }

    var data = pair[index];

    var mesh = drawable.Mesh;
    if (mesh.IndexCount < 1) throw new ArgumentNullException("mesh does not have indices", nameof(mesh));

    var cmd = new VkDrawIndexedIndirectCommand {
      indexCount = (uint)mesh.Indices.Length,
      instanceCount = 1,
      firstIndex = vertexBinding.FirstIndexOffset,
      vertexOffset = (int)vertexBinding.FirstVertexOffset,
      firstInstance = instanceIndex + additionalIndexOffset
    };

    pair[index].Commands.Add(cmd);

    instanceIndex++;
    data.CurrentIndexOffset += vertexBinding.FirstIndexOffset;
  }

  private unsafe void CreateIndexBuffer(ReadOnlySpan<IDrawable2D> drawables) {
    _globalIndexBuffer?.Dispose();

    var adjustedIndices = new List<uint>();
    uint vertexOffset = 0;

    for (int i = 0; i < drawables.Length; i++) {
      var mesh = drawables[i].Mesh;
      if (mesh.IndexCount < 1) continue;

      foreach (var idx in mesh.Indices) {
        adjustedIndices.Add(idx + vertexOffset);
      }

      vertexOffset += (uint)mesh.Vertices.Length;
    }

    var indexByteSize = (ulong)adjustedIndices.Count * sizeof(uint);

    var stagingBuffer = new NekoBuffer(
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

    _globalIndexBuffer = new NekoBuffer(
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
  private unsafe void CreateVertexBuffer(ReadOnlySpan<IDrawable2D> drawables) {
    _vertexBindings.Clear();
    // _bufferPool?.Dispose();
    _bufferPool?.Flush();
    _bufferPool ??= new BufferPool(_device, _allocator);
    _indirectData.Clear();
    _indirectData = [];

    uint currentPool = 0;
    uint indexOffset = 0;
    uint vertexOffset = 0;

    var previousSize = 0ul;

    _drawableCache = drawables.ToArray();
    foreach (var drawable in drawables) {
      var verts = drawable.Mesh!.Vertices;
      var byteSize = (ulong)verts.Length * (ulong)Unsafe.SizeOf<Vertex>();

      var indices = drawable.Mesh!.Indices;
      var byteSizeIndices = (ulong)indices.Length * sizeof(uint);

      var staging = new NekoBuffer(
                _allocator, _device, byteSize,
                BufferUsage.TransferSrc,
                MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
                stagingBuffer: true, cpuAccessible: true
              );
      staging.Map(byteSize);

      fixed (Vertex* p = verts) {
        staging.WriteToBuffer((nint)p, byteSize);

        if (!_bufferPool.AddToBuffer(currentPool, (nint)p, byteSize, previousSize, out var byteOffset, out var reason)) {
          var r = reason;
          currentPool = (uint)_bufferPool.AddToPool();
          _bufferPool.AddToBuffer(currentPool, (nint)p, byteSize, previousSize, out byteOffset, out reason);
        }
        previousSize += byteSize;

        _vertexBindings.Add(new VertexBinding {
          BufferIndex = currentPool,
          // FirstVertexOffset = (uint)(byteSize / (ulong)Unsafe.SizeOf<Vertex>()),
          // FirstIndexOffset = (uint)(indexByteOffset / (ulong)Unsafe.SizeOf<uint>()),
          FirstVertexOffset = (uint)(byteOffset / (ulong)Unsafe.SizeOf<Vertex>()),
          FirstIndexOffset = indexOffset
        });

        AddIndirectCommand(currentPool, drawable, _vertexBindings.Last(), ref _indirectData, ref _instanceIndex);

        indexOffset += (uint)indices.Length;
        vertexOffset += (uint)verts.Length;

        staging.Dispose();
      }
    }
  }

  public override unsafe void Dispose() {
    _device.WaitQueue();
    _globalVertexBuffer?.Dispose();
    _globalIndexBuffer?.Dispose();
    foreach (var buff in _indirectBuffers) {
      buff.Dispose();
    }
    base.Dispose();
  }
}
