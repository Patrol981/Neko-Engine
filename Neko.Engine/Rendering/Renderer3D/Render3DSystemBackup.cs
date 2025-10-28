// using System.Numerics;
// using System.Runtime.CompilerServices;
// using Neko.AbstractionLayer;
// using Neko.EntityComponentSystem;
// using Neko.Rendering.Renderer3D.Animations;
// using Neko.Vulkan;
// using Vortice.Vulkan;

// namespace Neko.Rendering.Renderer3D;

// public partial class Render3DSystem {
//   public unsafe void RenderSimple__(FrameInfo frameInfo, Span<Node> nodes) {
//     BindPipeline(frameInfo.CommandBuffer, Simple3D);
//     // … bind global descriptor sets …

//     // we assume _vertexBindings.Count == nodes.Length
//     uint lastBuffer = uint.MaxValue;
//     for (int i = 0; i < nodes.Length; i++) {
//       // grab this node’s assignment
//       var bindInfo = _vertexComplexBindings[i];
//       if (bindInfo.BufferIndex != lastBuffer) {
//         // bind the correct buffer
//         var vkb = _complexBufferPool.GetVertexBuffer(bindInfo.BufferIndex);
//         _renderer.CommandList.BindVertex(
//           frameInfo.CommandBuffer,
//           vkb,
//           0
//         );
//         lastBuffer = bindInfo.BufferIndex;
//       }

//       // bind the mesh’s own index-buffer & draw
//       nodes[i].BindNode(frameInfo.CommandBuffer);
//       // offset the firstVertex parameter:
//       nodes[i].DrawNode(
//         frameInfo.CommandBuffer,
//         (int)bindInfo.FirstVertexOffset,
//         (uint)i
//       );
//     }
//   }

//   public unsafe void RenderSimple_(FrameInfo frameInfo, Span<Node> nodes) {
//     BindPipeline(frameInfo.CommandBuffer, Simple3D);

//     Descriptor.BindDescriptorSet(_textureManager.AllTexturesDescriptor, frameInfo, _pipelines[Simple3D].PipelineLayout, 0, 1);
//     Descriptor.BindDescriptorSet(frameInfo.GlobalDescriptorSet, frameInfo, _pipelines[Simple3D].PipelineLayout, 1, 1);
//     Descriptor.BindDescriptorSet(frameInfo.ObjectDataDescriptorSet, frameInfo, _pipelines[Simple3D].PipelineLayout, 2, 1);
//     Descriptor.BindDescriptorSet(frameInfo.PointLightsDescriptorSet, frameInfo, _pipelines[Simple3D].PipelineLayout, 3, 1);

//     ulong lastVtxCount = 0;

//     for (int i = 0; i < nodes.Length; i++) {
//       if (nodes[i].ParentRenderer.GetOwner().CanBeDisposed || !nodes[i].ParentRenderer.GetOwner().Active) continue;
//       if (!nodes[i].ParentRenderer.FinishedInitialization) continue;

//       if (lastVtxCount != nodes[i].Mesh!.VertexCount) {
//         nodes[i].BindNode(frameInfo.CommandBuffer);
//         lastVtxCount = nodes[i].Mesh!.VertexCount;
//       }
//       nodes[i].DrawNode(frameInfo.CommandBuffer, (uint)i);
//     }
//   }

//   public unsafe void Render_Indirect2(FrameInfo frameInfo) {
//     BindPipeline(frameInfo.CommandBuffer, Skinned3D);

//     Descriptor.BindDescriptorSet(_textureManager.AllTexturesDescriptor, frameInfo, _pipelines[Skinned3D].PipelineLayout, 0, 1);
//     Descriptor.BindDescriptorSet(frameInfo.GlobalDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 1, 1);
//     Descriptor.BindDescriptorSet(frameInfo.ObjectDataDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 2, 1);
//     Descriptor.BindDescriptorSet(frameInfo.PointLightsDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 3, 1);
//     Descriptor.BindDescriptorSet(frameInfo.JointsBufferDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 4, 1);

//     // _renderer.CommandList.BindVertex(frameInfo.CommandBuffer, _globalVertexBuffer, 0);
//     // _renderer.CommandList.BindIndex(frameInfo.CommandBuffer, _globalIndexBuffer!, 0);
//     // _renderer.CommandList.DrawIndexedIndirect(
//     //   frameInfo.CommandBuffer,
//     //   _indirectBuffer!.GetBuffer(),
//     //   0,
//     //   (uint)_indirectDrawCommands.Count,
//     //   (uint)Unsafe.SizeOf<VkDrawIndexedIndirectCommand>()
//     // );

//     // Logger.Info("render");
//   }

//   public unsafe void Render_Ind(FrameInfo frameInfo) {
//     // Bind pipeline for skinned rendering (as seen in your method)
//     BindPipeline(frameInfo.CommandBuffer, Skinned3D);

//     // Bind descriptor sets (these remain unchanged)
//     Descriptor.BindDescriptorSet(_textureManager.AllTexturesDescriptor, frameInfo, _pipelines[Skinned3D].PipelineLayout, 0, 1);
//     Descriptor.BindDescriptorSet(frameInfo.GlobalDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 1, 1);
//     Descriptor.BindDescriptorSet(frameInfo.ObjectDataDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 2, 1);
//     Descriptor.BindDescriptorSet(frameInfo.PointLightsDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 3, 1);
//     Descriptor.BindDescriptorSet(frameInfo.JointsBufferDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 4, 1);

//     // Bind the index buffer
//     // _renderer.CommandList.BindIndex(frameInfo.CommandBuffer, _globalIndexBuffer!, 0);

//     // Loop through the indirect draw commands and bind vertex buffers accordingly
//     uint lastBufferIndex = uint.MaxValue;  // We use this to check when to rebind the vertex buffer

//     for (int i = 0; i < _indirectDrawCommands.Count; i++) {
//       var drawCommand = _indirectDrawCommands[i];
//       var bindInfo = _vertexComplexBindings[i]; // Get the binding info for this command

//       // Ensure correct offset within the buffer before issuing the draw call
//       var vertexOffset = bindInfo.FirstVertexOffset;

//       // If the buffer index changes, bind the new vertex buffer
//       if (bindInfo.BufferIndex != lastBufferIndex) {
//         // Get the appropriate vertex buffer from the pool
//         var vertexBuffer = _complexBufferPool.GetVertexBuffer(bindInfo.BufferIndex);
//         _renderer.CommandList.BindVertex(frameInfo.CommandBuffer, vertexBuffer, 0);
//         lastBufferIndex = bindInfo.BufferIndex;
//       }

//       // Ensure that the firstIndex for the draw call is correctly calculated
//       var firstIndex = drawCommand.firstIndex + vertexOffset;

//       // Now, perform the draw call using the indirect command
//       // _renderer.CommandList.DrawIndexedIndirect(
//       //     frameInfo.CommandBuffer,
//       //     _indirectBuffer!.GetBuffer(),
//       //     (ulong)(i * Unsafe.SizeOf<VkDrawIndexedIndirectCommand>()), // Correct offset in the indirect buffer
//       //     1,  // Always 1 instance per command (could vary for instancing)
//       //     (uint)Unsafe.SizeOf<VkDrawIndexedIndirectCommand>()  // Size of a single indirect command
//       // );
//     }
//   }

//   public unsafe void RenderComplex_(FrameInfo frameInfo, Span<Node> nodes, int offset) {
//     BindPipeline(frameInfo.CommandBuffer, Skinned3D);

//     Descriptor.BindDescriptorSet(_textureManager.AllTexturesDescriptor, frameInfo, _pipelines[Skinned3D].PipelineLayout, 0, 1);
//     Descriptor.BindDescriptorSet(frameInfo.GlobalDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 1, 1);
//     Descriptor.BindDescriptorSet(frameInfo.ObjectDataDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 2, 1);
//     Descriptor.BindDescriptorSet(frameInfo.PointLightsDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 3, 1);
//     Descriptor.BindDescriptorSet(frameInfo.JointsBufferDescriptorSet, frameInfo, _pipelines[Skinned3D].PipelineLayout, 4, 1);

//     ulong lastVtxCount = 0;

//     for (int i = 0; i < nodes.Length; i++) {
//       if (nodes[i].ParentRenderer.GetOwner().CanBeDisposed || !nodes[i].ParentRenderer.GetOwner().Active) continue;
//       if (!nodes[i].ParentRenderer.FinishedInitialization) continue;

//       if (i <= nodes.Length && nodes[i].ParentRenderer.Animations.Count > 0) {
//         nodes[i].ParentRenderer.GetOwner().TryGetComponent<AnimationController>()?.Update(nodes[i]);
//       }

//       if (lastVtxCount != nodes[i].Mesh!.VertexCount) {
//         nodes[i].BindNode(frameInfo.CommandBuffer);
//         lastVtxCount = nodes[i].Mesh!.VertexCount;
//       }

//       nodes[i].DrawNode(frameInfo.CommandBuffer, (uint)i + (uint)offset);
//     }
//   }

//   private unsafe void CreateVertexBuffer_(ReadOnlySpan<Entity> drawables) {
//     // _globalVertexBuffer?.Dispose();
//     List<Vertex> vertices = [];

//     for (int i = 0; i < drawables.Length; i++) {
//       foreach (var node in drawables[i].GetComponent<MeshRenderer>().MeshedNodes) {
//         var buffer = node.Mesh?.VertexBuffer;
//         if (buffer != null) {
//           vertices.AddRange(node.Mesh!.Vertices);
//         }
//       }
//     }

//     var vtxSize = (ulong)vertices.Count * (ulong)Unsafe.SizeOf<Vertex>();

//     var stagingBuffer = new NekoBuffer(
//       _allocator,
//       _device,
//       vtxSize,
//       BufferUsage.TransferSrc,
//       MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
//       stagingBuffer: true,
//       cpuAccessible: true
//     );

//     stagingBuffer.Map(vtxSize);
//     fixed (Vertex* pVertices = vertices.ToArray()) {
//       stagingBuffer.WriteToBuffer((nint)pVertices, vtxSize);
//     }

//     // _globalVertexBuffer = new NekoBuffer(
//     //   _allocator,
//     //   _device,
//     //   vtxSize,
//     //   (ulong)vertices.Count,
//     //   BufferUsage.VertexBuffer | BufferUsage.TransferDst,
//     //   MemoryProperty.DeviceLocal
//     // );

//     // _device.CopyBuffer(stagingBuffer.GetBuffer(), _globalVertexBuffer.GetBuffer(), vtxSize);
//     stagingBuffer.Dispose();
//   }
// }