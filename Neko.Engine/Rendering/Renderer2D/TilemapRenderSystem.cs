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

public sealed class TilemapRenderSystem : SystemBase {
  // Tilemap Layer Data
  public IDrawable2D[] LayerCache { get; private set; } = [];
  public int LastKnownLayerCount { get; private set; } = 0;
  private BufferPool? _layerPool;
  private Dictionary<uint, IndirectData> _layerIndirectData = [];
  private List<VertexBinding> _layerVertexBindings = [];
  private uint _layerInstnaceIndex;
  private List<VkDrawIndexedIndirectCommand> _layerDrawCommands = [];
  private NekoBuffer[] _layerIndirectBuffers = [];
  private SpritePushConstant140[] _layerShaderData = [];

  public TilemapRenderSystem(
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
      externalLayouts["TileLayerData"],
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
  }

  public void Refresh(ReadOnlySpan<IDrawable2D> layers) {
    LayerCache = [.. layers];
  }

  public void Update(FrameInfo frameInfo) {
    UpdateLayers(frameInfo);
  }

  private void UpdateLayers(FrameInfo frameInfo) {
    if (_layerShaderData.Length == 0) return;
    if (_layerShaderData.Length != LayerCache.Length) return;

    for (int i = 0; i < LayerCache.Length; i++) {
      var target = LayerCache[i];

      var texId = GetIndexOfMyTexture(target.Texture.TextureName);
      if (!texId.HasValue) throw new ArgumentException("", paramName: texId.ToString());

      if (target.LocalZDepth != 0) {
        _layerShaderData[i].SpriteMatrix = target.Entity
          .GetTransform()?
          .Matrix()
          .OverrideZDepth(target.LocalZDepth) ?? Matrix4x4.Identity;
      } else {
        _layerShaderData[i].SpriteMatrix = target.Entity
          .GetTransform()?
          .Matrix() ?? Matrix4x4.Identity;
      }

      _layerShaderData[i].SpriteSheetData.X = target.SpriteSheetSize.X;
      _layerShaderData[i].SpriteSheetData.Y = target.SpriteSheetSize.Y;
      _layerShaderData[i].SpriteSheetData.Z = target.SpriteIndex;
      _layerShaderData[i].SpriteSheetData.W = target.FlipX ? 1.0f : 0.0f;

      _layerShaderData[i].SpriteSheetData2.X = target.FlipY ? 1.0f : 0.0f;
      _layerShaderData[i].SpriteSheetData2.Y = texId.Value;
      _layerShaderData[i].SpriteSheetData2.Z = -1.0f;
      _layerShaderData[i].SpriteSheetData2.W = -1.0f;
    }

    unsafe {
      fixed (SpritePushConstant140* pLayerData = _layerShaderData) {
        _application.StorageCollection.WriteBuffer(
        "TileLayerStorage",
        frameInfo.FrameIndex,
        (nint)pLayerData,
        (ulong)Unsafe.SizeOf<SpritePushConstant140>() * (ulong)_layerShaderData.Length
      );
      }
    }
  }

  public void Render(FrameInfo frameInfo) {
    RenderLayers(frameInfo);
  }

  private void RenderLayers(FrameInfo frameInfo) {
    CreateOrUpdateBuffers(LayerCache);

    if (_layerPool == null) return;

    BindPipeline(frameInfo.CommandBuffer);

    Descriptor.BindDescriptorSet(_device, frameInfo.GlobalDescriptorSet, frameInfo, PipelineLayout, 0, 1);
    Descriptor.BindDescriptorSet(_device, _textureManager.AllTexturesDescriptor, frameInfo, PipelineLayout, 2, 1);
    Descriptor.BindDescriptorSet(_device, frameInfo.TileLayerDataDescriptorSet, frameInfo, PipelineLayout, 1, 1);

    foreach (var layerContainer in _layerIndirectData) {
      var targetVertex = _layerPool.GetVertexBuffer(layerContainer.Key);
      var targetIndex = _layerPool.GetIndexBuffer(layerContainer.Key);

      if (targetIndex == null || targetVertex == null) continue;

      _renderer.CommandList.BindIndex(frameInfo.CommandBuffer, targetIndex, 0);
      _renderer.CommandList.BindVertex(frameInfo.CommandBuffer, targetVertex, 0);

      _renderer.CommandList.DrawIndexedIndirect(
        frameInfo.CommandBuffer,
        _layerIndirectBuffers[layerContainer.Key].GetBuffer(),
        0,
        (uint)layerContainer.Value.Commands.Count,
        (uint)Unsafe.SizeOf<VkDrawIndexedIndirectCommand>()
      );
    }
  }

  private void CreateOrUpdateBuffers(ReadOnlySpan<IDrawable2D> tileLayers) {
    if (LastKnownLayerCount != tileLayers.Length) {
      Logger.Info($"Recreating Layer Buffers [{LastKnownLayerCount}] != [{tileLayers.Length}]");
      CreateVertexIndexBuffers(
        ref _layerPool,
        LayerCache,
        ref _layerIndirectData,
        ref _layerVertexBindings,
        ref _layerInstnaceIndex
      );
      CreateIndirectBuffer(ref _layerIndirectData, ref _layerIndirectBuffers);
      CreateIndirectCommands(LayerCache, ref _layerDrawCommands);
      _layerShaderData = new SpritePushConstant140[LayerCache.Length];
      Array.Fill(_layerShaderData, new());
      LastKnownLayerCount = tileLayers.Length;
    }
  }

  private unsafe void CreateIndirectBuffer(
    ref Dictionary<uint, IndirectData> pair,
    ref NekoBuffer[] buffArray
  ) {
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

  private void CreateIndirectCommands(
    ReadOnlySpan<IDrawable2D> drawables,
    ref List<VkDrawIndexedIndirectCommand> drawCommands
  ) {
    drawCommands.Clear();
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

      drawCommands.Add(cmd);
      indexOffset += (uint)mesh.Indices.Length;
    }
  }

  private void CreateVertexIndexBuffers(
    ref BufferPool? dstPool,
    in ReadOnlySpan<IDrawable2D> drawables,
    ref Dictionary<uint, IndirectData> indirectData,
    ref List<VertexBinding> vertexBindings,
    ref uint instanceIndex
  ) {
    dstPool?.Dispose();
    dstPool = new BufferPool(_device, _allocator);
    indirectData.Clear();
    vertexBindings.Clear();
    instanceIndex = 0;

    var indexOffset = 0u;
    var vertexOffset = 0u;

    var accumulatedIndexSize = 0u;
    var accumulatedVertexSize = 0ul;

    var currentPool = 0u;
    dstPool.CreateNewBakeData(currentPool);

    foreach (var drawable in drawables) {
      var mesh = drawable.Mesh;
      var verts = mesh.Vertices;
      var indices = mesh.Indices;

      var vertexByteSize = (ulong)verts.Length * (ulong)Unsafe.SizeOf<Vertex>();
      var indexByteSize = (uint)indices.Length * sizeof(uint);

      accumulatedVertexSize += vertexByteSize;
      accumulatedIndexSize += indexByteSize;

      var canAddVertex = dstPool.CanBakeMoreVertex(currentPool, accumulatedVertexSize);
      var canAddIndex = dstPool.CanBakeMoreIndex(currentPool, accumulatedIndexSize);

      if (!canAddIndex || !canAddVertex) {
        currentPool++;
        dstPool.CreateNewBakeData(currentPool);
        indexOffset = 0;
        vertexOffset = 0;
        accumulatedIndexSize = 0;
        accumulatedVertexSize = 0;
      }

      dstPool.AddIndexToBake(currentPool, [.. indices]);
      dstPool.AddVertexToBake(currentPool, [.. verts]);

      vertexBindings.Add(new VertexBinding {
        BufferIndex = currentPool,
        FirstVertexOffset = vertexOffset,
        FirstIndexOffset = indexOffset
      });

      AddIndirectCommand(
        currentPool,
        drawable, vertexBindings.Last(),
        ref indirectData,
        ref instanceIndex
      );

      indexOffset += (uint)indices.Length;
      vertexOffset += (uint)verts.Length;
    }

    dstPool.BakeAll();
  }

  private static int AddIndirectCommand(
    uint index,
    IDrawable2D drawable,
    VertexBinding vertexBinding,
    ref Dictionary<uint, IndirectData> pair,
    ref uint instanceIndex,
    in uint additionalIndexOffset = 0
  ) {
    if (!pair.TryGetValue(index, out var data)) {
      data = new IndirectData();
      pair[index] = data;
    }

    var mesh = drawable.Mesh;
    if (mesh.IndexCount < 1) throw new ArgumentNullException("mesh does not have indices", nameof(mesh));

    var cmd = new VkDrawIndexedIndirectCommand {
      indexCount = (uint)mesh.Indices.Length,
      instanceCount = 1,
      firstIndex = vertexBinding.FirstIndexOffset,
      vertexOffset = (int)vertexBinding.FirstVertexOffset,
      firstInstance = instanceIndex + additionalIndexOffset
    };

    data.Commands.Add(cmd);
    int cmdIdx = data.Commands.Count - 1;

    instanceIndex++;
    data.CurrentIndexOffset += vertexBinding.FirstIndexOffset;

    return cmdIdx;
  }

  private static int? GetIndexOfMyTexture(string texName) {
    return Application.Instance.TextureManager.PerSceneLoadedTextures
      .Where(x => x.Value.TextureName == texName)
      .FirstOrDefault()
      .Value.TextureManagerIndex;
  }

  public override void Dispose() {
    _device.WaitQueue();
    base.Dispose();
  }
}