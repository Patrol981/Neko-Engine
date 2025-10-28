using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Neko.Vulkan;

using Vortice.Vulkan;

namespace Neko.Rendering.Renderer3D;

public class PipelineModelProvider : VkPipelineProvider {
  public override unsafe VkVertexInputBindingDescription* GetBindingDescsFunc() {
    var bindingDescriptions = new VkVertexInputBindingDescription[1];
    bindingDescriptions[0].binding = 0;
    bindingDescriptions[0].stride = (uint)Unsafe.SizeOf<Vertex>();
    bindingDescriptions[0].inputRate = VkVertexInputRate.Vertex;
    fixed (VkVertexInputBindingDescription* ptr = bindingDescriptions) {
      return ptr;
    }
  }

  public override unsafe VkVertexInputAttributeDescription* GetAttribDescsFunc() {
    var attributeDescriptions = new VkVertexInputAttributeDescription[GetAttribsLength()];
    attributeDescriptions[0].binding = 0;
    attributeDescriptions[0].location = 0;
    attributeDescriptions[0].format = VkFormat.R32G32B32Sfloat;
    attributeDescriptions[0].offset = (uint)Marshal.OffsetOf<Vertex>("Position");

    attributeDescriptions[1].binding = 0;
    attributeDescriptions[1].location = 1;
    attributeDescriptions[1].format = VkFormat.R32G32B32Sfloat;
    attributeDescriptions[1].offset = (uint)Marshal.OffsetOf<Vertex>("Color");

    attributeDescriptions[2].binding = 0;
    attributeDescriptions[2].location = 2;
    attributeDescriptions[2].format = VkFormat.R32G32B32Sfloat;
    attributeDescriptions[2].offset = (uint)Marshal.OffsetOf<Vertex>("Normal");

    attributeDescriptions[3].binding = 0;
    attributeDescriptions[3].location = 3;
    attributeDescriptions[3].format = VkFormat.R32G32Sfloat;
    attributeDescriptions[3].offset = (uint)Marshal.OffsetOf<Vertex>("Uv");

    attributeDescriptions[4].binding = 0;
    attributeDescriptions[4].location = 4;
    attributeDescriptions[4].format = VkFormat.R32G32B32A32Sint;
    attributeDescriptions[4].offset = (uint)Marshal.OffsetOf<Vertex>("JointIndices");

    attributeDescriptions[5].binding = 0;
    attributeDescriptions[5].location = 5;
    attributeDescriptions[5].format = VkFormat.R32G32B32A32Sfloat;
    attributeDescriptions[5].offset = (uint)Marshal.OffsetOf<Vertex>("JointWeights");

    fixed (VkVertexInputAttributeDescription* ptr = attributeDescriptions) {
      return ptr;
    }
  }

  public override uint GetAttribsLength() {
    return 6;
  }

  public override uint GetBindingsLength() {
    return 1;
  }
}
