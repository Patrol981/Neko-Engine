using Neko.AbstractionLayer;
using Neko.Metal;
using Neko.Vulkan;

namespace Neko.Rendering;

public static class RendererFactory {
  public static IRenderer CreateAPIRenderer(Application app) {
    switch (app.CurrentAPI) {
      case RenderAPI.Vulkan:
        return new VkDynamicRenderer(app);
      case RenderAPI.Metal:
        return new MRenderer(app);
      default:
        throw new NotImplementedException("Factory tried to create renderer that is not supported");
    }
  }
}