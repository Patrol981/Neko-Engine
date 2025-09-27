using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Dwarf.EntityComponentSystem;
using Dwarf.Extensions.Logging;
using Dwarf.Globals;
using Dwarf.Rendering.Renderer3D;

namespace Dwarf.Rendering.Renderer3D.Animations;

public class AnimationController {
  public Entity Owner { get; init; }

  private readonly Dictionary<string, Animation> _animations = [];
  private string _currentAnimation = "";

  private readonly MeshRenderer _meshRenderer;

  public AnimationController(Entity owner, Animation[] animations, MeshRenderer meshRenderer) {
    Owner = owner;
    foreach (var animation in animations) {
      _animations.Add(animation.Name, animation);
    }
    _meshRenderer = meshRenderer;
  }

  public void PlayAnimation(string animationName) {
    _currentAnimation = animationName;
  }

  public Animation? GetAnimation(string animationName) {
    _animations.TryGetValue(animationName, out var animation);
    if (animation != null) {
      return animation;
    }
    Logger.Error("No animation found");
    return null;
  }

  public ReadOnlySpan<string> EnumerateAnimations() {
    return _animations.Keys.ToArray();
  }

  [MethodImpl(MethodImplOptions.AggressiveOptimization)]
  public void Update(Node node) {
    if (node == null) return;
    if (_animations.Count < 1) return;
    if (_currentAnimation == string.Empty) return;

    node.AnimationTimer += Time.DeltaTimeRender;

    float adjustedTimer = node.AnimationTimer;
    if (_animations[_currentAnimation].End > 0) {
      adjustedTimer %= _animations[_currentAnimation].End;
    }

    UpdateAnimation(_animations[_currentAnimation], adjustedTimer, 0);
  }

  [MethodImpl(MethodImplOptions.AggressiveOptimization)]
  public void UpdateAnimation(Animation animation, float time, float weight) {
    var channels = CollectionsMarshal.AsSpan(animation.Channels);
    foreach (var channel in channels) {
      var sampler = animation.Samplers[channel.SamplerIndex];
      var inputs = CollectionsMarshal.AsSpan(sampler.Inputs);
      if (sampler.Inputs.Count > sampler.OutputsVec4.Count) {
        continue;
      }
      for (int i = 0; i < inputs.Length - 1; i++) {
        if ((time >= inputs[i]) && (time <= inputs[i + 1])) {
          float u = MathF.Max(0.0f, time - inputs[i]) / (inputs[i + 1] - inputs[i]);
          if (u <= 1.0f) {
            switch (channel.Path) {
              case AnimationChannel.PathType.Translation:
                sampler.Translate(i, time, ref channel.Node);
                break;
              case AnimationChannel.PathType.Rotation:
                sampler.Rotate(i, time, ref channel.Node);
                break;
              case AnimationChannel.PathType.Scale:
                sampler.Scale(i, time, ref channel.Node);
                break;
            }
          }
        }
      }
    }

    foreach (var node in _meshRenderer.Nodes) {
      node.Update();
    }
  }
}