using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Neko;
using Neko.AbstractionLayer;
using Neko.EntityComponentSystem;
using Neko.Extensions.Logging;
using Neko.Globals;
using Neko.Rendering;
using Neko.Rendering.Renderer2D.Interfaces;
using Neko.Rendering.Renderer2D.Models;
using Neko.Vulkan;
using ZLinq;

namespace Neko.Rendering.Renderer2D;

public class CustomShaderRender2DSystem : SystemBase, IRenderSystem {
  private struct CustomShaderBuffer {
    public required Guid EntityId { get; set; }
    public required Guid BufferId { get; set; }

    public NekoBuffer VertexBuffer { get; set; }
    public NekoBuffer IndexBuffer { get; set; }

    public required uint IndexCount { get; set; }

    public required string PipelineName { get; set; }

    public Guid ShaderTextureId { get; set; }
  };

  private readonly IDescriptorSetLayout[] _basicLayouts = [];

  public int LastKnownElemCount { get; set; } = 0;
  private List<CustomShaderBuffer> _buffers = [];

  private Dictionary<Guid, SpritePushConstant140> _objectDataArray = [];

  public CustomShaderRender2DSystem(
    Application app,
    nint allocator,
    VulkanDevice device,
    IRenderer renderer,
    TextureManager textureManager,
    Dictionary<string, IDescriptorSetLayout> externalLayouts,
    IPipelineConfigInfo configInfo = null!
  ) : base(app, allocator, device, renderer, textureManager, configInfo) {
    _basicLayouts = [
      externalLayouts["Global"],
      externalLayouts["CustomSpriteData"],
      _textureManager.AllTexturesSetLayout,
    ];
  }

  public void Setup(ReadOnlySpan<IDrawable2D> spritesWithCustomShaders) {
    foreach (var sprite in spritesWithCustomShaders) {
      var pipelineName = sprite.CustomShader.Name;

      AddPipelineData(new() {
        RenderPass = _application.Renderer.GetSwapchainRenderPass(),
        VertexName = $"{pipelineName}_vertex",
        FragmentName = $"{pipelineName}_fragment",
        PipelineProvider = new PipelineSpriteProvider(),
        DescriptorSetLayouts = [.. _basicLayouts],
        PipelineName = pipelineName
      });
    }
  }

  private static int GetIndexOfMyTexture(string texName) {
    var texturePair = Application.Instance.TextureManager.PerSceneLoadedTextures
      .Where(x => x.Value.TextureName == texName)
      .Single();
    return texturePair.Value.TextureManagerIndex;
  }

  public void Update(
    FrameInfo frameInfo,
    ReadOnlySpan<IDrawable2D> spritesWithCustomShaders,
    in HashSet<Entity> entities
  ) {
    AddOrUpdateBuffers(spritesWithCustomShaders);

    for (ushort i = 0; i < _buffers.Count; i++) {
      var entity = entities.Where(x => x.Id == _buffers[i].EntityId).First();
      var transform = entity.GetTransform();
      var drawable = entity.GetDrawable2D();
      var myTexId = GetIndexOfMyTexture(drawable?.Texture.TextureName ?? "");

      ref SpritePushConstant140 spriteData = ref CollectionsMarshal.GetValueRefOrAddDefault(
        _objectDataArray,
        _buffers[i].BufferId,
        out var exists
      );

      if (!exists) continue;

      spriteData.SpriteMatrix = transform?.Matrix() ?? Matrix4x4.Identity;
      spriteData.SpriteSheetData.X = drawable?.SpriteSheetSize.X ?? 0;
      spriteData.SpriteSheetData.Y = drawable?.SpriteSheetSize.Y ?? 0;
      spriteData.SpriteSheetData.Z = drawable?.SpriteIndex ?? 0;
      spriteData.SpriteSheetData.W = ((drawable?.FlipX) ?? false) ? 1 : 0;
      spriteData.SpriteSheetData2.X = ((drawable?.FlipY) ?? false) ? 1 : 0;
      spriteData.SpriteSheetData2.Y = myTexId;

      if (spriteData.SpriteSheetData2.Z >= 60.0f) {
        spriteData.SpriteSheetData2.Z = 0.0f;
      }
      spriteData.SpriteSheetData2.Z += Time.DeltaTime;

      if (_buffers[i].ShaderTextureId != Guid.Empty) {
        var texture = _textureManager.GetTextureLocal(_buffers[i].ShaderTextureId);
        var customTexture = GetIndexOfMyTexture(texture.TextureName);
        spriteData.SpriteSheetData2.W = customTexture;
      } else {
        Logger.Warn("Texture not set for buffId " + i);
        spriteData.SpriteSheetData2.W = -1;
      }
    }

    unsafe {
      fixed (SpritePushConstant140* pSpriteData = _objectDataArray.Values.AsValueEnumerable().ToArray()) {
        _application.StorageCollection.WriteBuffer(
          "CustomSpriteStorage",
          frameInfo.FrameIndex,
          (nint)pSpriteData,
          (ulong)Unsafe.SizeOf<SpritePushConstant140>() * (ulong)_objectDataArray.Values.Count
        );
      }
    }
  }

  public void Render(FrameInfo frameInfo) {
    string currentPipelineName = "";
    foreach (var buffer in _buffers) {
      if (currentPipelineName != buffer.PipelineName) {
        currentPipelineName = buffer.PipelineName;
        BindPipeline(frameInfo.CommandBuffer, buffer.PipelineName);

        Descriptor.BindDescriptorSet(_device, frameInfo.GlobalDescriptorSet, frameInfo, _pipelines[currentPipelineName].PipelineLayout, 0, 1);
        Descriptor.BindDescriptorSet(_device, frameInfo.CustomSpriteDataDescriptorSet, frameInfo, _pipelines[currentPipelineName].PipelineLayout, 1, 1);
        Descriptor.BindDescriptorSet(_device, _textureManager.AllTexturesDescriptor, frameInfo, _pipelines[currentPipelineName].PipelineLayout, 2, 1);
      }

      _renderer.CommandList.BindIndex(frameInfo.CommandBuffer, buffer.IndexBuffer, 0);
      _renderer.CommandList.BindVertex(frameInfo.CommandBuffer, buffer.VertexBuffer, 0);

      _renderer.CommandList.DrawIndexed(
        commandBuffer: frameInfo.CommandBuffer,
        indexCount: buffer.IndexCount,
        instanceCount: 1,
        firstIndex: 0,
        vertexOffset: 0,
        firstInstance: 0
      );
    }
  }

  private void AddOrUpdateBuffers(
    ReadOnlySpan<IDrawable2D> spritesWithCustomShaders
  ) {
    if (LastKnownElemCount == spritesWithCustomShaders.Length) {
      return;
    }
    LastKnownElemCount = spritesWithCustomShaders.Length;

    foreach (var buff in _buffers) {
      buff.VertexBuffer?.Dispose();
      buff.IndexBuffer?.Dispose();
    }
    _buffers.Clear();
    _objectDataArray.Clear();

    foreach (var drawable in spritesWithCustomShaders) {
      CreateIndexBuffer(drawable.Mesh, out var indexBuffer);
      CreateVertexBuffer(drawable.Mesh, out var vertexBuffer);

      var bufferId = Guid.NewGuid();

      _buffers.Add(new CustomShaderBuffer() {
        EntityId = drawable.Entity.Id,
        BufferId = bufferId,
        VertexBuffer = vertexBuffer,
        IndexBuffer = indexBuffer,
        IndexCount = (uint)drawable.Mesh.IndexCount,
        PipelineName = drawable.CustomShader.Name,
        ShaderTextureId = drawable.CustomShader.ShaderTextureId
      });

      _objectDataArray.TryAdd(bufferId, default);
    }
  }

  private unsafe void CreateVertexBuffer(in Mesh mesh, out NekoBuffer vertexBuffer) {
    var byteSize = (ulong)Unsafe.SizeOf<Vertex>() * mesh.VertexCount;

    var stagingBuffer = new NekoBuffer(
      _allocator,
      _device,
      byteSize,
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
      stagingBuffer: true,
      cpuAccessible: true
    );

    stagingBuffer.Map();
    fixed (Vertex* vertexData = mesh.Vertices) {
      stagingBuffer.WriteToBuffer((nint)vertexData, byteSize);
    }
    stagingBuffer.Unmap();

    vertexBuffer = new NekoBuffer(
      _allocator,
      _device,
      byteSize,
      BufferUsage.TransferDst | BufferUsage.VertexBuffer,
      MemoryProperty.DeviceLocal
    );

    _device.CopyBuffer(stagingBuffer.GetBuffer(), vertexBuffer.GetBuffer(), stagingBuffer.GetBufferSize());
    stagingBuffer.Dispose();
  }

  private unsafe void CreateIndexBuffer(in Mesh mesh, out NekoBuffer indexBuffer) {
    var byteSize = sizeof(uint) * mesh.IndexCount;

    var stagingBuffer = new NekoBuffer(
      _allocator,
      _device,
      byteSize,
      BufferUsage.TransferSrc,
      MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
      stagingBuffer: true,
      cpuAccessible: true
    );

    stagingBuffer.Map(byteSize);
    fixed (uint* indexData = mesh.Indices) {
      stagingBuffer.WriteToBuffer((nint)indexData, byteSize);
    }
    stagingBuffer.Unmap();

    indexBuffer = new NekoBuffer(
      _allocator,
      _device,
      (ulong)Unsafe.SizeOf<uint>(),
      mesh.IndexCount,
      BufferUsage.IndexBuffer | BufferUsage.TransferDst,
      MemoryProperty.DeviceLocal
    );

    _device.CopyBuffer(
      stagingBuffer.GetBuffer(),
      indexBuffer.GetBuffer(),
      byteSize
    );
    stagingBuffer.Dispose();

  }
}