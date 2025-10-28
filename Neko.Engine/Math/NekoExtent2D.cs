using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Runtime.InteropServices;

using Vortice.Vulkan;

namespace Neko.Math;

public static class NekoExtentExtensions {
  public static NekoExtent2D FromVkExtent2D(this VkExtent2D vkExtent2D) {
    return new NekoExtent2D(vkExtent2D.width, vkExtent2D.height);
  }

  public static VkExtent2D FromNekoExtent2D(this NekoExtent2D NekoExtent2D) {
    return new VkExtent2D(NekoExtent2D.Width, NekoExtent2D.Height);
  }
}

[StructLayout(LayoutKind.Sequential)]
public struct NekoExtent2D : IEquatable<NekoExtent2D> {
  public uint Width;
  public uint Height;

  public NekoExtent2D(uint width, uint height) {
    Width = width;
    Height = height;
  }

  public NekoExtent2D(int width, int height) {
    Width = (uint)width;
    Height = (uint)height;
  }

  /// <inheritdoc/>
  public override readonly bool Equals([NotNullWhen(true)] object? obj) => obj is NekoExtent2D other && Equals(other);

  /// <inheritdoc/>
  public readonly bool Equals(NekoExtent2D other) => Width == other.Width && Height == other.Height;

  /// <inheritdoc/>
  public override readonly int GetHashCode() => HashCode.Combine(Width, Height);

  /// <inheritdoc/>
  public override readonly string ToString() => $"{{Width={Width},Height={Height}}}";

  /// <summary>
  /// Compares two <see cref="NekoExtent2D"/> objects for equality.
  /// </summary>
  /// <param name="left">The <see cref="NekoExtent2D"/> on the left hand of the operand.</param>
  /// <param name="right">The <see cref="NekoExtent2D"/> on the right hand of the operand.</param>
  /// <returns>
  /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
  /// </returns>
  public static bool operator ==(NekoExtent2D left, NekoExtent2D right) => left.Equals(right);

  /// <summary>
  /// Compares two <see cref="NekoExtent2D"/> objects for inequality.
  /// </summary>
  /// <param name="left">The <see cref="NekoExtent2D"/> on the left hand of the operand.</param>
  /// <param name="right">The <see cref="NekoExtent2D"/> on the right hand of the operand.</param>
  /// <returns>
  /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
  /// </returns>
  public static bool operator !=(NekoExtent2D left, NekoExtent2D right) => !left.Equals(right);

  /// <summary>
  /// Performs an implicit conversion from <see cre ="VkExtent2D"/> to <see cref="Size" />.
  /// </summary>
  /// <param name="value">The value to convert.</param>
  /// <returns>The result of the conversion.</returns>
  public static implicit operator Size(NekoExtent2D value) => new((int)value.Width, (int)value.Height);

  /// <summary>
  /// Performs an implicit conversion from <see cre ="Size"/> to <see cref="NekoExtent2D" />.
  /// </summary>
  /// <param name="value">The value to convert.</param>
  /// <returns>The result of the conversion.</returns>
  public static implicit operator NekoExtent2D(Size value) => new(value.Width, value.Height);

  public VkExtent2D ToVkExtent2D() {
    return new VkExtent2D(Width, Height);
  }

  public static NekoExtent2D Zero => default;
}
