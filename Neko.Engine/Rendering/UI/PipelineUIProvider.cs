using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Neko.Rendering;
using Neko.Vulkan;

using Vortice.Vulkan;

namespace Neko;

public class PipelineUIProvider : VkPipelineProvider {
  public override unsafe VkVertexInputBindingDescription* GetBindingDescsFunc() {
    var bindingDescriptions = new VkVertexInputBindingDescription[1];
    bindingDescriptions[0].binding = 0;
    bindingDescriptions[0].stride = ((uint)Unsafe.SizeOf<Vertex>());
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

    fixed (VkVertexInputAttributeDescription* ptr = attributeDescriptions) {
      return ptr;
    }
  }

  public override uint GetAttribsLength() {
    return 4;
  }

  public override uint GetBindingsLength() {
    return 1;
  }
}
