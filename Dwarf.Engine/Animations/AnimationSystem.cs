using Dwarf.AbstractionLayer;
using Dwarf.EntityComponentSystem;
using Dwarf.Rendering;
using Dwarf.Rendering.Renderer3D;
using ZLinq;

namespace Dwarf.Animations;

public class AnimationSystem : SystemBase {
  public AnimationSystem(
    Application app,
    nint allocator,
    IDevice device,
    IRenderer renderer,
    TextureManager textureManager,
    IPipelineConfigInfo configInfo = null!
  ) : base(app, allocator, device, renderer, textureManager, configInfo) {
  }

  public void Update(ValueEnumerable<ZLinq.Linq.FromArray<Node>, Node> animatedNodes) {
    // Parallel.ForEach(animatedNodes, node => {
    //   var owner = node.ParentRenderer.Owner;
    //   if (owner.CanBeDisposed) return;
    //   owner.GetAnimationController()?.Update(node);
    // });
    // for (int i = 0; i < animatedNodes.Length; i++) {
    //   var n = animatedNodes[i];
    //   var owner = n.ParentRenderer.Owner;
    //   if (owner.CanBeDisposed) continue;
    //   owner.GetAnimationController()?.Update(n);
    // }
    foreach (var node in animatedNodes) {
      var owner = node.ParentRenderer.Owner;
      if (owner.CanBeDisposed) return;
      owner.GetAnimationController()?.Update(node);
    }
  }
}