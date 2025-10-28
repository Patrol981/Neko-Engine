using System.Numerics;

using Neko.EntityComponentSystem;
using Neko.Globals;
using Neko.Math;
using Neko.Utils;
using Neko.Windowing;

namespace Neko.Rendering.UI;

public enum Anchor {
  Right,
  Left,
  Middle,
  Bottom,
  Top,
  RightTop,
  RightBottom,
  LeftTop,
  LeftBottom,
  MiddleTop,
  MiddleBottom
}

public enum ResolutionAspect {
  Aspect4to3,
  Aspect8to5,
  Aspect16to9,
  Aspect21to9
}

public class Resolution {
  public Vector2 Size { get; private set; }
  public ResolutionSize ResolutionSize { get; private set; }
  public ResolutionAspect ResolutionAspect { get; private set; }

  public Resolution(Vector2 size, ResolutionSize resolutionSize, ResolutionAspect resolutionAspect) {
    Size = size;
    ResolutionSize = resolutionSize;
    ResolutionAspect = resolutionAspect;
  }
}

public enum ResolutionSize {
  Screen800x600,
  Screen1024x600,
  Screen1334x750,
  Screen1280x800,
  Screen1600x900,
  Screen1920x1080,
  Screen2560x1080
}