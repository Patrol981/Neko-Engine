using System.Numerics;
using System.Runtime.CompilerServices;
using Dwarf.Loaders;

namespace Dwarf.Rendering.Renderer3D.Animations;

public class AnimationSampler : ICloneable {
  public enum InterpolationType {
    Linear,
    Step,
    CubicSpline
  }

  public InterpolationType Interpolation;
  public List<float> Inputs = [];
  public List<Vector4> OutputsVec4 = [];
  public List<float> Outputs = [];

  public Vector4 CubicSplineInterpolation(int idx, float time, int stride) {
    float delta = Inputs[idx + 1] - Inputs[idx];
    float t = (time - Inputs[idx]) / delta;
    var current = idx * stride * 3;
    var next = (idx + 1) * stride * 3;
    var A = 0;
    var V = stride * 1;
    var B = stride * 2;

    float t2 = MathF.Pow(t, 2);
    float t3 = MathF.Pow(t, 3);
    var pt = Vector4.Zero;
    for (int i = 0; i < stride; i++) {
      float p0 = Outputs[current + i + V];      // starting point at t = 0
      float m0 = delta * Outputs[current + i + A];  // scaled starting tangent at t = 0
      float p1 = Outputs[next + i + V];       // ending point at t = 1
      float m1 = delta * Outputs[next + i + B];   // scaled ending tangent at t = 1
      pt[i] = ((2.0f * t3 - 3.0f * t2 + 1.0f) * p0) + ((t3 - 2.0f * t2 + t) * m0) + ((-2.0f * t3 + 3.0f * t2) * p1) + ((t3 - t2) * m0);
    }
    return pt;
  }

  public void Translate(int idx, float time, ref Node node) {
    switch (Interpolation) {
      case InterpolationType.Linear:
        float u = MathF.Max(0.0f, time - Inputs[idx]) / (Inputs[idx + 1] - Inputs[idx]);
        var newTranslation = Vector4.Lerp(OutputsVec4[idx], OutputsVec4[idx + 1], u).ToVector3();
        node.Translation = newTranslation;
        break;
      case InterpolationType.Step:
        newTranslation = OutputsVec4[idx].ToVector3();
        node.Translation = newTranslation;
        break;
      case InterpolationType.CubicSpline:
        newTranslation = CubicSplineInterpolation(idx, time, 3).ToVector3();
        node.Translation = newTranslation;
        break;
    }
  }

  public void Translate_Old(int idx, float time, ref Node node, float weight) {
    var blendedTranslation = node.Translation;
    switch (Interpolation) {
      case InterpolationType.Linear:
        float u = MathF.Max(0.0f, time - Inputs[idx]) / (Inputs[idx + 1] - Inputs[idx]);
        var newTranslation = Vector4.Lerp(OutputsVec4[idx], OutputsVec4[idx + 1], u).ToVector3();
        node.Translation = Vector3.Lerp(blendedTranslation, newTranslation, weight);
        // node.Translation = newTranslation;
        break;
      case InterpolationType.Step:
        newTranslation = OutputsVec4[idx].ToVector3();
        // node.Translation = Vector3.Lerp(blendedTranslation, newTranslation, weight);
        node.Translation = newTranslation;
        break;
      case InterpolationType.CubicSpline:
        newTranslation = CubicSplineInterpolation(idx, time, 3).ToVector3();
        node.Translation = Vector3.Lerp(blendedTranslation, newTranslation, weight);
        // node.Translation = newTranslation;
        break;
    }
  }

  public void Scale(int idx, float time, ref Node node) {
    switch (Interpolation) {
      case InterpolationType.Linear:
        float u = MathF.Max(0.0f, time - Inputs[idx]) / (Inputs[idx + 1] - Inputs[idx]);
        node.Scale = Vector4.Lerp(OutputsVec4[idx], OutputsVec4[idx + 1], u).ToVector3();
        break;
      case InterpolationType.Step:
        node.Scale.X = OutputsVec4[idx].X;
        node.Scale.Y = OutputsVec4[idx].Y;
        node.Scale.Z = OutputsVec4[idx].Z;
        break;
      case InterpolationType.CubicSpline:
        node.Scale.X = OutputsVec4[idx].X;
        node.Scale.Y = OutputsVec4[idx].Y;
        node.Scale.Z = OutputsVec4[idx].Z;
        break;
    }
  }

  public void Scale_Old(int idx, float time, ref Node node, float weight) {
    var blendedScale = node.Scale;
    switch (Interpolation) {
      case InterpolationType.Linear:
        float u = MathF.Max(0.0f, time - Inputs[idx]) / (Inputs[idx + 1] - Inputs[idx]);
        var newScale = Vector4.Lerp(OutputsVec4[idx], OutputsVec4[idx + 1], u).ToVector3();
        node.Scale = Vector3.Lerp(blendedScale, newScale, weight);
        // node.Scale = newScale;
        break;
      case InterpolationType.Step:
        newScale = OutputsVec4[idx].ToVector3();
        node.Scale = Vector3.Lerp(blendedScale, newScale, weight);
        // node.Scale = newScale;
        break;
      case InterpolationType.CubicSpline:
        newScale = CubicSplineInterpolation(idx, time, 3).ToVector3();
        node.Scale = Vector3.Lerp(blendedScale, newScale, weight);
        // node.Scale = newScale;
        break;
    }
  }

  public void Rotate(int idx, float time, ref Node node) {
    switch (Interpolation) {
      case InterpolationType.Linear:
        // float u = MathF.Max(0.0f, time - Inputs[idx]) / (Inputs[idx + 1] - Inputs[idx]);
        node.Rotation = InterpRotation(time, Inputs.ToArray(), OutputsVec4.ToArray(), idx);
        break;
      case InterpolationType.Step:
        node.Rotation.X = OutputsVec4[idx].X;
        node.Rotation.Y = OutputsVec4[idx].Y;
        node.Rotation.Z = OutputsVec4[idx].Z;
        node.Rotation.W = OutputsVec4[idx].W;
        break;
      case InterpolationType.CubicSpline:
        node.Rotation.X = OutputsVec4[idx].X;
        node.Rotation.Y = OutputsVec4[idx].Y;
        node.Rotation.Z = OutputsVec4[idx].Z;
        node.Rotation.W = OutputsVec4[idx].W;
        break;
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static Quaternion InterpRotation(
    float time,
    float[] Inputs,
    Vector4[] OutputsVec4,
    int idx) {
    // Cache to avoid repeating indexer + bounds checks
    float t0 = Inputs[idx];
    float t1 = Inputs[idx + 1];

    float denom = t1 - t0;
    float u = denom != 0f ? MathF.Max(0f, time - t0) / denom : 0f;

    // Take refs to avoid copying Vector4s unnecessarily
    ref readonly Vector4 v1 = ref OutputsVec4[idx];
    ref readonly Vector4 v2 = ref OutputsVec4[idx + 1];

    // These are structs -> no heap allocations
    Quaternion q1 = new Quaternion(v1.X, v1.Y, v1.Z, v1.W);
    Quaternion q2 = new Quaternion(v2.X, v2.Y, v2.Z, v2.W);

    return Quaternion.Slerp(q1, q2, u);
  }

  public void Rotate_Old(int idx, float time, ref Node node, float weight) {
    var blendedRotation = node.Rotation;
    switch (Interpolation) {
      case InterpolationType.Linear:
        float u = MathF.Max(0.0f, time - Inputs[idx]) / (Inputs[idx + 1] - Inputs[idx]);
        var quat1 = new Quaternion(
          OutputsVec4[idx].X,
          OutputsVec4[idx].Y,
          OutputsVec4[idx].Z,
          OutputsVec4[idx].W
        );
        var quat2 = new Quaternion(
          OutputsVec4[idx + 1].X,
          OutputsVec4[idx + 1].Y,
          OutputsVec4[idx + 1].Z,
          OutputsVec4[idx + 1].W
        );
        var newRotation = Quaternion.Slerp(quat1, quat2, u);
        node.Rotation = Quaternion.Slerp(blendedRotation, Quaternion.Normalize(newRotation), weight);

        // node.Rotation = Quaternion.Normalize(Quaternion.Slerp(quat1, quat2, u));
        break;
      case InterpolationType.Step:
        newRotation = new Quaternion(
          OutputsVec4[idx].X,
          OutputsVec4[idx].Y,
          OutputsVec4[idx].Z,
          OutputsVec4[idx].W
        );
        node.Rotation = Quaternion.Slerp(blendedRotation, newRotation, weight);
        // node.Rotation = newRotation;
        break;
      case InterpolationType.CubicSpline:
        var rot = CubicSplineInterpolation(idx, time, 4);
        newRotation = new Quaternion(
          rot.X,
          rot.Y,
          rot.Z,
          rot.W
        );
        node.Rotation = Quaternion.Slerp(blendedRotation, Quaternion.Normalize(newRotation), weight);
        // node.Rotation = Quaternion.Normalize(newRotation);
        break;
    }
  }

  public object Clone() {
    return new AnimationSampler {
      Interpolation = Interpolation,
      Inputs = Inputs,
      OutputsVec4 = OutputsVec4,
      Outputs = Outputs,
    };
  }
}
