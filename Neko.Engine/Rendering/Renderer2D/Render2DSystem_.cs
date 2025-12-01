// using System.Diagnostics;
// using System.Numerics;
// using System.Runtime.CompilerServices;
// using System.Runtime.InteropServices;
// using Neko.AbstractionLayer;
// using Neko.EntityComponentSystem;
// using Neko.Extensions.Logging;
// using Neko.Rendering.Renderer2D.Interfaces;
// using Neko.Rendering.Renderer2D.Models;
// using Neko.Vulkan;
// using Vortice.Vulkan;

// namespace Neko.Rendering.Renderer2D;

// public readonly struct CmdRef(uint pool, int cmdIndex) {
//   public readonly uint Pool = pool;
//   public readonly int CmdIndex = cmdIndex;
// }

// public class Render2DSystem : SystemBase {
//   // private NekoBuffer? _globalVertexBuffer;
//   // private NekoBuffer? _globalIndexBuffer;
//   private NekoBuffer[] _indirectBuffers = [];
//   private Dictionary<uint, IndirectData> _indirectData = [];
//   private List<VkDrawIndexedIndirectCommand> _indirectDrawCommands = [];
//   private IDrawable2D[] _drawableCache = [];
//   private uint _instanceIndex = 0;

//   private BufferPool _bufferPool = null!;
//   private List<VertexBinding> _vertexBindings = [];
//   private bool _invalid = false;
//   private bool _cacheMatch = false;
//   private SpritePushConstant140[] _spriteData = [];
//   private readonly Dictionary<IDrawable2D, CmdRef> _cmdMap = [];

//   private IDrawable2D[] _visibleCache = [];
//   private readonly Dictionary<uint, List<VkDrawIndexedIndirectCommand>> _visibleScratch = [];
//   private readonly Dictionary<IDrawable2D, int> _drawableToObjIndex = [];
//   private readonly Dictionary<Guid, float> _texIndexCache = [];

//   public int LastKnownElemCount { get; private set; } = 0;

//   public Render2DSystem(
//     Application app,
//     nint allocator,
//     VulkanDevice device,
//     IRenderer renderer,
//     TextureManager textureManager,
//     Dictionary<string, IDescriptorSetLayout> externalLayouts,
//     IPipelineConfigInfo configInfo = null!
//   ) : base(app, allocator, device, renderer, textureManager, configInfo) {
//     IDescriptorSetLayout[] descriptorSetLayouts = [
//       externalLayouts["Global"],
//       externalLayouts["SpriteData"],
//       _textureManager.AllTexturesSetLayout,
//     ];

//     AddPipelineData(new() {
//       RenderPass = renderer.GetSwapchainRenderPass(),
//       VertexName = "sprite_vertex",
//       FragmentName = "sprite_fragment",
//       PipelineProvider = new PipelineSpriteProvider(),
//       DescriptorSetLayouts = descriptorSetLayouts,
//     });

//     _descriptorPool = new VulkanDescriptorPool.Builder(_device)
//       .SetMaxSets(CommonConstants.MAX_SETS)
//       .AddPoolSize(DescriptorType.UniformBuffer, CommonConstants.MAX_SETS)
//       .AddPoolSize(DescriptorType.SampledImage, CommonConstants.MAX_SETS)
//       .AddPoolSize(DescriptorType.Sampler, CommonConstants.MAX_SETS)
//       .AddPoolSize(DescriptorType.InputAttachment, CommonConstants.MAX_SETS)
//       .AddPoolSize(DescriptorType.StorageBuffer, CommonConstants.MAX_SETS)
//       .SetPoolFlags(DescriptorPoolCreateFlags.UpdateAfterBind)
//       .Build();
//   }

//   public void Setup(ReadOnlySpan<IDrawable2D> drawables, ref TextureManager textures) {
//     if (drawables.Length < 1) {
//       Logger.Warn("Entities that are capable of using 2D renderer are less than 1, thus 2D Render System won't be recreated");
//       return;
//     }

//     LastKnownElemCount = CalculateTextureCount(drawables);
//   }

//   public bool CheckSizes(ReadOnlySpan<IDrawable2D> drawables) {
//     // if (_texturesCount == -1) {
//     //   var textureManager = Application.Instance.TextureManager;
//     //   // Setup(drawables, ref textureManager);
//     // }
//     var newCount = CalculateTextureCount(drawables);
//     // var newCount = CalculateLastKnownElemCount(drawables);
//     if (newCount != LastKnownElemCount) {
//       // LastKnownElemCount = newCount;
//       return false;
//     }

//     return true;
//   }
//   private static int CalculateTextureCount(ReadOnlySpan<IDrawable2D> drawables) {
//     int count = 0;
//     for (int i = 0; i < drawables.Length; i++) {
//       count += drawables[i].SpriteCount;
//     }
//     return count;
//   }

//   private static int CalculateLastKnownElemCount(ReadOnlySpan<IDrawable2D> drawables) {
//     int count = 0;
//     for (int i = 0; i < drawables.Length; i++) {
//       if (drawables[i].Children.Length > 0) {
//         count += drawables[i].Children.Length;
//       } else {
//         count++;
//       }
//     }

//     return count;

//     // return drawables.Length;
//   }

//   public void Invalidate(ReadOnlySpan<IDrawable2D> drawables) {
//     _invalid = true;
//     _drawableCache = [.. drawables];
//   }

//   public unsafe void Update(FrameInfo frameInfo) {
//     if (_spriteData.Length == 0) return;

//     for (int i = 0; i < _visibleCache.Length; i++) {
//       var target = _visibleCache[i];

//       var myTexId = GetIndexOfMyTexture(target.Texture.TextureName);
//       // if (!myTexId.HasValue) throw new ArgumentException("", paramName: myTexId.ToString());

//       if (target.LocalZDepth != 0) {
//         // spriteData[i] = new() {
//         //   SpriteMatrix = target.Entity.GetTransform()?.Matrix().OverrideZDepth(target.LocalZDepth) ?? Matrix4x4.Identity,
//         //   SpriteSheetData = new(target.SpriteSheetSize.X, target.SpriteSheetSize.Y, target.SpriteIndex, target.FlipX ? 1.0f : 0.0f),
//         //   SpriteSheetData2 = new(target.FlipY ? 1.0f : 0.0f, myTexId.Value, -1, -1)
//         // };
//         _spriteData[i].SpriteMatrix = target.Entity.GetTransform()?.Matrix().OverrideZDepth(target.LocalZDepth) ?? Matrix4x4.Identity;

//         _spriteData[i].SpriteSheetData.X = target.SpriteSheetSize.X;
//         _spriteData[i].SpriteSheetData.Y = target.SpriteSheetSize.Y;
//         _spriteData[i].SpriteSheetData.Z = target.SpriteIndex;
//         _spriteData[i].SpriteSheetData.W = target.FlipX ? 1.0f : 0.0f;

//         _spriteData[i].SpriteSheetData2.X = target.FlipY ? 1.0f : 0.0f;
//         _spriteData[i].SpriteSheetData2.Y = myTexId;
//         _spriteData[i].SpriteSheetData2.Z = -1.0f;
//         _spriteData[i].SpriteSheetData2.W = -1.0f;

//       } else {
//         // spriteData[i] = new() {
//         //   SpriteMatrix = target.Entity.GetTransform()?.Matrix() ?? Matrix4x4.Identity,
//         //   SpriteSheetData = new(target.SpriteSheetSize.X, target.SpriteSheetSize.Y, target.SpriteIndex, target.FlipX ? 1.0f : 0.0f),
//         //   SpriteSheetData2 = new(target.FlipY ? 1.0f : 0.0f, myTexId.Value, -1, -1)
//         // };

//         _spriteData[i].SpriteMatrix = target.Entity.GetTransform()?.Matrix() ?? Matrix4x4.Identity;

//         _spriteData[i].SpriteSheetData.X = target.SpriteSheetSize.X;
//         _spriteData[i].SpriteSheetData.Y = target.SpriteSheetSize.Y;
//         _spriteData[i].SpriteSheetData.Z = target.SpriteIndex;
//         _spriteData[i].SpriteSheetData.W = target.FlipX ? 1.0f : 0.0f;

//         _spriteData[i].SpriteSheetData2.X = target.FlipY ? 1.0f : 0.0f;
//         _spriteData[i].SpriteSheetData2.Y = myTexId;
//         _spriteData[i].SpriteSheetData2.Z = -1.0f;
//         _spriteData[i].SpriteSheetData2.W = -1.0f;
//       }
//     }

//     fixed (SpritePushConstant140* pSpriteData = _spriteData) {
//       _application.StorageCollection.WriteBuffer(
//         "SpriteStorage",
//         frameInfo.FrameIndex,
//         (nint)pSpriteData,
//         (ulong)Unsafe.SizeOf<SpritePushConstant140>() * (ulong)_spriteData.Length
//       );
//     }
//   }

//   public void Render(FrameInfo frameInfo) {
//     AddOrUpdateBuffers(_drawableCache);
//     var visible = RefillIndirectBuffersWithCulling();
//     if (visible < 1) return;

//     var canProceed =
//       // _globalIndexBuffer != null &&
//       frameInfo.GlobalDescriptorSet.IsNotNull &&
//       frameInfo.SpriteDataDescriptorSet.IsNotNull;

//     if (!canProceed) return;

//     BindPipeline(frameInfo.CommandBuffer);

//     Descriptor.BindDescriptorSet(_device, frameInfo.GlobalDescriptorSet, frameInfo, PipelineLayout, 0, 1);
//     Descriptor.BindDescriptorSet(_device, _textureManager.AllTexturesDescriptor, frameInfo, PipelineLayout, 2, 1);
//     Descriptor.BindDescriptorSet(_device, frameInfo.SpriteDataDescriptorSet, frameInfo, PipelineLayout, 1, 1);

//     // _renderer.CommandList.BindIndex(frameInfo.CommandBuffer, _globalIndexBuffer!, 0);

//     foreach (var container in _indirectData) {
//       var targetVertex = _bufferPool.GetVertexBuffer(container.Key);
//       var targetIndex = _bufferPool.GetIndexBuffer(container.Key);

//       _renderer.CommandList.BindIndex(frameInfo.CommandBuffer, targetIndex, 0);
//       _renderer.CommandList.BindVertex(frameInfo.CommandBuffer, targetVertex, 0);

//       _renderer.CommandList.DrawIndexedIndirect(
//         frameInfo.CommandBuffer,
//         _indirectBuffers[container.Key].GetBuffer(),
//         0,
//         (uint)container.Value.Commands.Count,
//         (uint)Unsafe.SizeOf<VkDrawIndexedIndirectCommand>()
//       );
//     }
//   }

//   public unsafe void Render_(FrameInfo frameInfo, ReadOnlySpan<IDrawable2D> drawables) {
//     BindPipeline(frameInfo.CommandBuffer);
//     Descriptor.BindDescriptorSet(_device, frameInfo.GlobalDescriptorSet, frameInfo, PipelineLayout, 0, 1);
//     Descriptor.BindDescriptorSet(_device, _textureManager.AllTexturesDescriptor, frameInfo, PipelineLayout, 2, 1);
//     Descriptor.BindDescriptorSet(_device, frameInfo.SpriteDataDescriptorSet, frameInfo, PipelineLayout, 1, 1);
//     // _renderer.CommandList.BindVertex(frameInfo.CommandBuffer, _globalVertexBuffer, 0);
//     // _renderer.CommandList.BindIndex(frameInfo.CommandBuffer, _globalIndexBuffer!, 0);

//     uint indexOffset = 0;

//     for (uint instance = 0; instance < (uint)_drawableCache.Length; instance++) {
//       var d = _drawableCache[(int)instance];
//       var mesh = d.Mesh;

//       var bindInfo = _vertexBindings[(int)instance];
//       var buffer = _bufferPool.GetVertexBuffer(bindInfo.BufferIndex);
//       _renderer.CommandList.BindVertex(frameInfo.CommandBuffer, buffer, 0);

//       uint thisCount = (uint)mesh.Indices.Length;

//       if (!d.Active || d.Entity.CanBeDisposed || mesh.IndexCount < 1) {
//         indexOffset += thisCount;
//         continue;
//       }

//       _renderer.CommandList.DrawIndexed(
//         frameInfo.CommandBuffer,
//         indexCount: thisCount,
//         instanceCount: 1,
//         firstIndex: bindInfo.FirstIndexOffset,
//         vertexOffset: (int)bindInfo.FirstVertexOffset,
//         firstInstance: instance
//       );

//       indexOffset += thisCount;
//     }
//   }

//   private void AddOrUpdateBuffers(ReadOnlySpan<IDrawable2D> drawables) {
//     if (!_invalid && _bufferPool != null) return;
//     // if (LastKnownElemCount == drawables.Length) {
//     //   return;
//     // }
//     // LastKnownElemCount = drawables.Length;

//     _instanceIndex = 0;

//     // _globalIndexBuffer?.Dispose();
//     // foreach (var buff in _indirectBuffers) {
//     //   buff.Dispose();
//     // }

//     // CreateVertexBuffer(drawables);
//     // CreateIndexBuffer(drawables);
//     // CreateVertexIndexBuffer(drawables);
//     // CreateIndirectCommands(drawables);
//     // CreateIndirectBuffer(ref _indirectData, ref _indirectBuffers);

//     _invalid = false;
//     int totalObjs = drawables.Length;

//     _spriteData = new SpritePushConstant140[drawables.Length];
//     for (int i = 0; i < _spriteData.Length; i++) {
//       _spriteData[i] = new();
//     }

//     if (totalObjs > 0) {
//       CreateVertexIndexBuffer(drawables);
//     }

//     EnsureIndirectBuffers(_indirectData, ref _indirectBuffers);

//     for (int i = 0; i < drawables.Length; i++) {
//       var drawable = drawables[i];
//       var mesh = drawable.Mesh;

//       float texId = GetOrAddTextureIndex(mesh.TextureIdReference);

//       ref var sd = ref _spriteData[i];
//       sd.SpriteMatrix = drawable.Entity.GetTransform()?.Matrix() ?? Matrix4x4.Identity;

//       sd.SpriteSheetData.X = drawable.SpriteSheetSize.X;
//       sd.SpriteSheetData.Y = drawable.SpriteSheetSize.Y;
//       sd.SpriteSheetData.Z = drawable.SpriteIndex;
//       sd.SpriteSheetData.W = drawable.FlipX ? 1.0f : 0.0f;

//       sd.SpriteSheetData2.X = drawable.FlipY ? 1.0f : 0.0f;
//       sd.SpriteSheetData2.Y = texId;
//       sd.SpriteSheetData2.Z = -1.0f;
//       sd.SpriteSheetData2.W = -1.0f;
//     }

//     EnsureVisibleScratchCapacity();
//   }

//   private float GetOrAddTextureIndex(Guid textureId) {
//     ref float texId = ref CollectionsMarshal.GetValueRefOrAddDefault(
//       _texIndexCache,
//       textureId,
//       out var exists
//     );

//     if (!exists) {
//       var targetTexture = _textureManager.GetTextureLocal(textureId);
//       texId = GetIndexOfMyTexture(targetTexture.TextureName);
//     }

//     return texId;
//   }

//   // private static int? GetIndexOfMyTexture(string texName) {
//   //   return Application.Instance.TextureManager.PerSceneLoadedTextures.Where(x => x.Value.TextureName == texName).FirstOrDefault().Value.TextureManagerIndex;
//   // }

//   private static int GetIndexOfMyTexture(string texName) {
//     var texturePair = Application.Instance.TextureManager.PerSceneLoadedTextures
//       .Where(x => x.Value.TextureName == texName)
//       .Single();
//     return texturePair.Value.TextureManagerIndex;
//   }

//   private void CreateIndirectCommands(ReadOnlySpan<IDrawable2D> drawables) {
//     _indirectDrawCommands.Clear();
//     uint indexOffset = 0;

//     for (int i = 0; i < drawables.Length; i++) {
//       var mesh = drawables[i].Mesh;
//       if (mesh.IndexCount < 1)
//         continue;

//       var cmd = new VkDrawIndexedIndirectCommand {
//         indexCount = (uint)mesh.Indices.Length,
//         instanceCount = 1,
//         firstIndex = indexOffset,
//         vertexOffset = 0,
//         firstInstance = (uint)i
//       };

//       _indirectDrawCommands.Add(cmd);
//       indexOffset += (uint)mesh.Indices.Length;
//     }
//   }

//   private unsafe void CreateIndirectBuffer(ref Dictionary<uint, IndirectData> pair, ref NekoBuffer[] buffArray) {
//     foreach (var buff in buffArray) {
//       buff?.Dispose();
//     }
//     Array.Clear(buffArray);
//     buffArray = new NekoBuffer[pair.Keys.Count];
//     int i = 0;

//     foreach (var commands in pair) {
//       var size = commands.Value.Commands.Count * Unsafe.SizeOf<VkDrawIndexedIndirectCommand>();

//       var stagingBuffer = new NekoBuffer(
//         _allocator,
//         _device,
//         (ulong)size,
//         BufferUsage.TransferSrc,
//         MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
//         stagingBuffer: true,
//         cpuAccessible: true
//       );

//       stagingBuffer.Map((ulong)size);
//       fixed (VkDrawIndexedIndirectCommand* pIndirectCommands = commands.Value.Commands.ToArray()) {
//         stagingBuffer.WriteToBuffer((nint)pIndirectCommands, (ulong)size);
//       }

//       var inBuff = new NekoBuffer(
//         _allocator,
//         _device,
//         (ulong)size,
//         BufferUsage.TransferDst | BufferUsage.IndirectBuffer,
//         MemoryProperty.DeviceLocal
//       );

//       _device.CopyBuffer(stagingBuffer.GetBuffer(), inBuff.GetBuffer(), (ulong)size);
//       stagingBuffer.Dispose();

//       buffArray[i] = inBuff;
//       i++;
//     }
//   }

//   private static int AddIndirectCommand(
//     uint index,
//     IDrawable2D drawable,
//     VertexBinding vertexBinding,
//     ref Dictionary<uint, IndirectData> pair,
//     ref uint instanceIndex,
//     in uint additionalIndexOffset = 0
//   ) {
//     if (!pair.TryGetValue(index, out var data)) {
//       data = new IndirectData();
//       pair[index] = data;
//     }

//     var mesh = drawable.Mesh;
//     if (mesh.IndexCount < 1) throw new ArgumentNullException("mesh does not have indices", nameof(mesh));

//     var cmd = new VkDrawIndexedIndirectCommand {
//       indexCount = (uint)mesh.Indices.Length,
//       instanceCount = 1,
//       firstIndex = vertexBinding.FirstIndexOffset,
//       vertexOffset = (int)vertexBinding.FirstVertexOffset,
//       firstInstance = instanceIndex + additionalIndexOffset
//     };

//     data.Commands.Add(cmd);
//     int cmdIdx = data.Commands.Count - 1;

//     instanceIndex++;
//     data.CurrentIndexOffset += vertexBinding.FirstIndexOffset;

//     return cmdIdx;
//   }

//   private unsafe void CreateVertexIndexBuffer(ReadOnlySpan<IDrawable2D> drawables) {
//     _bufferPool?.Dispose();
//     _bufferPool = new BufferPool(_device, _allocator);
//     _indirectData.Clear();

//     var indexOffset = 0u;
//     var vertexOffset = 0u;

//     var accumulatedIndexSize = 0u;
//     var accumulatedVertexSize = 0ul;

//     var currentPool = 0u;
//     _bufferPool.CreateNewBakeData(currentPool);

//     _drawableCache = drawables.ToArray();
//     foreach (var drawable in drawables) {
//       var mesh = drawable.Mesh;
//       var verts = drawable.Mesh!.Vertices;
//       var indices = drawable.Mesh!.Indices;

//       var vertexByteSize = (ulong)verts.Length * (ulong)Unsafe.SizeOf<Vertex>();
//       var indexByteSize = (uint)indices.Length * sizeof(uint);

//       accumulatedVertexSize += vertexByteSize;
//       accumulatedIndexSize += indexByteSize;

//       var canAddVertex = _bufferPool.CanBakeMoreVertex(currentPool, accumulatedVertexSize);
//       var canAddIndex = _bufferPool.CanBakeMoreIndex(currentPool, accumulatedIndexSize);
//       var isOverflowingOnNextStep = CheckForOverflow(indexOffset, (uint)indices.Length);

//       if (!canAddIndex || !canAddVertex || isOverflowingOnNextStep) {
//         currentPool++;
//         _bufferPool.CreateNewBakeData(currentPool);
//         indexOffset = 0;
//         vertexOffset = 0;
//         accumulatedIndexSize = 0;
//         accumulatedVertexSize = 0;
//       }

//       // var localIndices = new List<uint>();
//       // foreach (var idx in meshes[node.MeshGuid].Indices) {
//       //   adjustedIndices.Add(idx + vertexOffset);
//       //   localIndices.Add(idx + vertexOffset);
//       // }

//       _bufferPool.AddIndexToBake(currentPool, [.. indices]);
//       _bufferPool.AddVertexToBake(currentPool, [.. verts]);

//       _vertexBindings.Add(new VertexBinding {
//         BufferIndex = currentPool,
//         FirstVertexOffset = vertexOffset,
//         FirstIndexOffset = indexOffset
//       });

//       var cmdIdx = AddIndirectCommand(
//         currentPool,
//         drawable, _vertexBindings.Last(),
//         ref _indirectData,
//         ref _instanceIndex
//       );
//       _cmdMap[drawable] = new CmdRef(currentPool, cmdIdx);

//       indexOffset += (uint)indices.Length;
//       vertexOffset += (uint)verts.Length;
//     }

//     _bufferPool.BakeAll();
//   }

//   private unsafe void EnsureIndirectBuffers(
//     Dictionary<uint, IndirectData> pair,
//     ref NekoBuffer[] buffArray
//   ) {
//     int maxKey = pair.Count == 0 ? -1 : (int)pair.Keys.Max();
//     if (maxKey < 0) { buffArray = []; return; }

//     if (buffArray == null || buffArray.Length <= maxKey) {
//       var newArr = new NekoBuffer[maxKey + 1];
//       if (buffArray != null) Array.Copy(buffArray, newArr, buffArray.Length);
//       buffArray = newArr;
//     }

//     foreach (var kv in pair) {
//       uint pool = kv.Key;
//       var data = kv.Value;
//       ulong neededBytes = (ulong)data.Commands.Count * (ulong)Unsafe.SizeOf<VkDrawIndexedIndirectCommand>();

//       var existing = buffArray[pool];
//       bool needsAlloc = existing == null || existing.GetBufferSize() < neededBytes;

//       if (!needsAlloc) continue;

//       existing?.Dispose();

//       var inBuff = new NekoBuffer(
//         _allocator,
//         _device,
//         neededBytes,
//         BufferUsage.IndirectBuffer,
//         MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
//         stagingBuffer: false,
//         cpuAccessible: true
//       );

//       inBuff.Map(neededBytes);

//       buffArray[pool] = inBuff;

//       var span = CollectionsMarshal.AsSpan(data.Commands);
//       if (span.Length > 0) {
//         ref var first = ref MemoryMarshal.GetReference(span);
//         fixed (VkDrawIndexedIndirectCommand* p = &first) {
//           inBuff.WriteToBuffer((nint)p, neededBytes, 0);
//           inBuff.Flush(neededBytes, 0);
//         }
//       }
//     }
//   }

//   private void EnsureVisibleScratchCapacity() {
//     foreach (var (pool, data) in _indirectData) {
//       if (!_visibleScratch.TryGetValue(pool, out var list))
//         _visibleScratch[pool] = new List<VkDrawIndexedIndirectCommand>(data.Commands.Count);
//       else if (list.Capacity < data.Commands.Count)
//         list.Capacity = data.Commands.Count;
//     }
//   }

//   private uint RefillIndirectBuffersWithCulling() {
//     foreach (var s in _visibleScratch.Values) s.Clear();

//     _visibleCache = [.. _drawableCache];

//     foreach (var n in _visibleCache) {
//       var owner = n.Entity;
//       if (owner.CanBeDisposed || !owner.Active) continue;

//       if (!_cmdMap.TryGetValue(n, out var r)) continue;
//       var src = _indirectData[r.Pool].Commands[r.CmdIndex];

//       if (!_visibleScratch.TryGetValue(r.Pool, out var list)) {
//         list = new List<VkDrawIndexedIndirectCommand>(32);
//         _visibleScratch[r.Pool] = list;
//       }
//       list.Add(src);
//     }

//     foreach (var kv in _visibleScratch) {
//       var pool = kv.Key;
//       var list = kv.Value;
//       _indirectData[pool].VisibleCount = list.Count;
//       var targetBuffer = _indirectBuffers![pool];
//       CopyCmdListToBuffer(list, targetBuffer);
//     }

//     uint total = 0;
//     foreach (var d in _indirectData.Values) total += (uint)d.VisibleCount;
//     return total;
//   }

//   private static unsafe void CopyCmdListToBuffer(List<VkDrawIndexedIndirectCommand> list, NekoBuffer buf) {
//     var span = CollectionsMarshal.AsSpan(list);
//     if (span.Length == 0) return;

//     ref var first = ref MemoryMarshal.GetReference(span);
//     fixed (VkDrawIndexedIndirectCommand* p = &first) {
//       ulong bytes = (ulong)span.Length * (ulong)Unsafe.SizeOf<VkDrawIndexedIndirectCommand>();
//       buf.WriteToBuffer((nint)p, bytes, 0);
//       buf.Flush(bytes, 0);
//     }
//   }

//   private static bool CheckForOverflow(uint a, uint b) {
//     if (a > uint.MaxValue - b) {
//       return true;
//     }
//     return false;
//   }

//   private unsafe void CreateIndexBuffer(ReadOnlySpan<IDrawable2D> drawables) {
//     // _globalIndexBuffer?.Dispose();

//     var adjustedIndices = new List<uint>();
//     uint vertexOffset = 0;

//     for (int i = 0; i < drawables.Length; i++) {
//       var mesh = drawables[i].Mesh;
//       if (mesh.IndexCount < 1) continue;

//       foreach (var idx in mesh.Indices) {
//         adjustedIndices.Add(idx + vertexOffset);
//       }

//       vertexOffset += (uint)mesh.Vertices.Length;
//     }

//     var indexByteSize = (ulong)adjustedIndices.Count * sizeof(uint);

//     var stagingBuffer = new NekoBuffer(
//       _allocator,
//       _device,
//       indexByteSize,
//       BufferUsage.TransferSrc,
//       MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
//       stagingBuffer: true,
//       cpuAccessible: true
//     );

//     stagingBuffer.Map(indexByteSize);
//     fixed (uint* pSrc = adjustedIndices.ToArray()) {
//       stagingBuffer.WriteToBuffer((nint)pSrc, indexByteSize);
//     }

//     // _globalIndexBuffer = new NekoBuffer(
//     //   _allocator,
//     //   _device,
//     //   (ulong)Unsafe.SizeOf<uint>(),
//     //   (uint)adjustedIndices.Count,
//     //   BufferUsage.IndexBuffer | BufferUsage.TransferDst,
//     //   MemoryProperty.DeviceLocal
//     // );

//     // _device.CopyBuffer(
//     //   stagingBuffer.GetBuffer(),
//     //   _globalIndexBuffer.GetBuffer(),
//     //   indexByteSize
//     // );
//     stagingBuffer.Dispose();
//   }
//   private unsafe void CreateVertexBuffer(ReadOnlySpan<IDrawable2D> drawables) {
//     _vertexBindings.Clear();
//     // _bufferPool?.Dispose();
//     _bufferPool?.Flush();
//     _bufferPool ??= new BufferPool(_device, _allocator);
//     _indirectData.Clear();
//     _indirectData = [];

//     uint currentPool = 0;
//     uint indexOffset = 0;
//     uint vertexOffset = 0;

//     var previousSize = 0ul;

//     _drawableCache = drawables.ToArray();
//     foreach (var drawable in drawables) {
//       var verts = drawable.Mesh!.Vertices;
//       var byteSize = (ulong)verts.Length * (ulong)Unsafe.SizeOf<Vertex>();

//       var indices = drawable.Mesh!.Indices;
//       var byteSizeIndices = (ulong)indices.Length * sizeof(uint);

//       var staging = new NekoBuffer(
//                 _allocator, _device, byteSize,
//                 BufferUsage.TransferSrc,
//                 MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
//                 stagingBuffer: true, cpuAccessible: true
//               );
//       staging.Map(byteSize);

//       fixed (Vertex* p = verts) {
//         staging.WriteToBuffer((nint)p, byteSize);

//         if (!_bufferPool.AddToBuffer(currentPool, (nint)p, byteSize, previousSize, out var byteOffset, out var reason)) {
//           var r = reason;
//           currentPool = (uint)_bufferPool.AddToPool();
//           _bufferPool.AddToBuffer(currentPool, (nint)p, byteSize, previousSize, out byteOffset, out reason);
//         }
//         previousSize += byteSize;

//         _vertexBindings.Add(new VertexBinding {
//           BufferIndex = currentPool,
//           // FirstVertexOffset = (uint)(byteSize / (ulong)Unsafe.SizeOf<Vertex>()),
//           // FirstIndexOffset = (uint)(indexByteOffset / (ulong)Unsafe.SizeOf<uint>()),
//           FirstVertexOffset = (uint)(byteOffset / (ulong)Unsafe.SizeOf<Vertex>()),
//           FirstIndexOffset = indexOffset
//         });

//         AddIndirectCommand(currentPool, drawable, _vertexBindings.Last(), ref _indirectData, ref _instanceIndex);

//         indexOffset += (uint)indices.Length;
//         vertexOffset += (uint)verts.Length;

//         staging.Dispose();
//       }
//     }
//   }

//   public override unsafe void Dispose() {
//     _device.WaitQueue();
//     // _globalVertexBuffer?.Dispose();
//     // _globalIndexBuffer?.Dispose();
//     _bufferPool?.Dispose();
//     foreach (var buff in _indirectBuffers) {
//       buff.Dispose();
//     }
//     base.Dispose();
//   }
// }
