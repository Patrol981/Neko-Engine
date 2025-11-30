using Neko.AbstractionLayer;
using Neko.EntityComponentSystem;
using Neko.Rendering;
using Neko.Rendering.Renderer3D;
using Neko.Vulkan;
using ZLinq;

namespace Neko.Animations;

public class AnimationSystem : SystemBase {

  public bool Enabled = true;

  public AnimationSystem(
    Application app,
    nint allocator,
    VulkanDevice device,
    IRenderer renderer,
    TextureManager textureManager,
    IPipelineConfigInfo configInfo = null!
  ) : base(app, allocator, device, renderer, textureManager, configInfo) {
  }

  public void Update(Node[] animatedNodes) {
    if (!Enabled) return;

    Parallel.ForEach(animatedNodes, node => {
      var owner = node.ParentRenderer.Owner;
      if (owner.CanBeDisposed) return;
      owner.GetAnimationController()?.Update(node);

      // var ctrl = owner.GetAnimationController();
    });
    // for (int i = 0; i < animatedNodes.Length; i++) {
    //   var n = animatedNodes[i];
    //   var owner = n.ParentRenderer.Owner;
    //   if (owner.CanBeDisposed) continue;
    //   owner.GetAnimationController()?.Update(n);
    // }
    // foreach (var node in animatedNodes) {
    //   var owner = node.ParentRenderer.Owner;
    //   if (owner.CanBeDisposed) return;
    //   owner.GetAnimationController()?.Update(node);
    // }
  }
}