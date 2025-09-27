using System.Numerics;

using Dwarf.AbstractionLayer;
using Dwarf.Extensions.Logging;
using Dwarf.Math;
using Dwarf.Utils;

using glTFLoader;
using glTFLoader.Schema;

namespace Dwarf.Loaders;

public static partial class GLTFLoaderKHR {
  private static uint[] GetIndexAccessor(Gltf gltf, byte[] globalBuffer, int accessorIdx) {
    var accessor = gltf.Accessors[accessorIdx];
    var bufferView = gltf.BufferViews[(int)accessor.BufferView!];
    var buffer = gltf.Buffers[bufferView.Buffer];

    uint[] indices;
    int byteOffset = bufferView.ByteOffset + accessor.ByteOffset;

    using var stream = new MemoryStream(globalBuffer);
    using var reader = new BinaryReader(stream);

    reader.BaseStream.Seek(byteOffset, SeekOrigin.Begin);
    indices = new uint[accessor.Count];
    for (int i = 0; i < accessor.Count; i++) {
      switch (accessor.ComponentType) {
        case Accessor.ComponentTypeEnum.UNSIGNED_BYTE:
          indices[i] = reader.ReadByte();
          break;
        case Accessor.ComponentTypeEnum.UNSIGNED_SHORT:
          indices[i] = reader.ReadUInt16();
          break;
        case Accessor.ComponentTypeEnum.UNSIGNED_INT:
          indices[i] = reader.ReadUInt32();
          break;
        default:
          throw new NotSupportedException("Unsupported index component type.");
      }
    }

    return indices;
  }
  public static float[] GetFloatAccessor(Gltf gltf, byte[] globalBuffer, Accessor accessor) {
    var bufferView = gltf.BufferViews[(int)accessor.BufferView!];

    var data = new float[accessor.Count];
    var byteOffset = bufferView.ByteOffset + accessor.ByteOffset;
    var stride = 4;

    using var stream = new MemoryStream(globalBuffer);
    using var reader = new BinaryReader(stream);

    reader.BaseStream.Seek(byteOffset, SeekOrigin.Begin);
    for (int i = 0; i < accessor.Count; i++) {
      data[i] = reader.ReadSingle();
      reader.BaseStream.Seek(stride - 4, SeekOrigin.Current);
    }

    return data;
  }
  public static void LoadAccessor<T>(Gltf gltf, byte[] globalBuffer, Accessor accessor, out T[][] data) {
    var bufferView = gltf.BufferViews[(int)accessor.BufferView!];

    data = new T[accessor.Count][];
    var byteOffset = bufferView.ByteOffset + accessor.ByteOffset;

    var typeResult = HandleType(accessor.Type, accessor.ComponentType);
    if (typeof(T) != typeResult.Item2)
      throw new ArgumentException($"{typeof(T)} does not match with {typeResult.Item2}");

    using var stream = new MemoryStream(globalBuffer);
    using var reader = new BinaryReader(stream);

    reader.BaseStream.Seek(byteOffset, SeekOrigin.Begin);

    for (int i = 0; i < accessor.Count; i++) {
      data[i] = new T[typeResult.Item1];
      for (int j = 0; j < typeResult.Item1; j++) {
        if (typeResult.Item2 == typeof(float)) {
          var value = reader.ReadSingle();
          data[i][j] = (T)(object)value;
        } else if (typeResult.Item2 == typeof(short)) {
          var value = reader.ReadInt16();
          data[i][j] = (T)(object)value;
        } else if (typeResult.Item2 == typeof(sbyte)) {
          var value = reader.ReadSByte();
          data[i][j] = (T)(object)value;
        } else if (typeResult.Item2 == typeof(uint)) {
          var value = reader.ReadUInt32();
          data[i][j] = (T)(object)value;
        } else if (typeResult.Item2 == typeof(byte)) {
          var value = reader.ReadByte();
          data[i][j] = (T)(object)value;
        } else if (typeResult.Item2 == typeof(ushort)) {
          var value = reader.ReadUInt16();
          data[i][j] = (T)(object)value;
        } else {
          throw new InvalidCastException($"Given type {typeResult.Item2} cannot be parsed!");
        }
      }
    }
  }
  private static (int, Type) HandleType(Accessor.TypeEnum type, Accessor.ComponentTypeEnum componentType) {
    Type valueType;
    int elemPerVec;

    switch (componentType) {
      case Accessor.ComponentTypeEnum.BYTE:
        valueType = typeof(sbyte);
        break;
      case Accessor.ComponentTypeEnum.SHORT:
        valueType = typeof(short);
        break;
      case Accessor.ComponentTypeEnum.FLOAT:
        valueType = typeof(float);
        break;
      case Accessor.ComponentTypeEnum.UNSIGNED_INT:
        valueType = typeof(uint);
        break;
      case Accessor.ComponentTypeEnum.UNSIGNED_BYTE:
        valueType = typeof(byte);
        break;
      case Accessor.ComponentTypeEnum.UNSIGNED_SHORT:
        valueType = typeof(ushort);
        break;
      default:
        Logger.Error("Unknown Component Type!");
        throw new ArgumentException($"Unknown Component Type! {nameof(componentType)}");
    }

    switch (type) {
      case Accessor.TypeEnum.SCALAR:
        elemPerVec = 1;
        break;
      case Accessor.TypeEnum.VEC2:
        elemPerVec = 2;
        break;
      case Accessor.TypeEnum.VEC3:
        elemPerVec = 3;
        break;
      case Accessor.TypeEnum.VEC4:
        elemPerVec = 4;
        break;
      case Accessor.TypeEnum.MAT2:
        elemPerVec = 4;
        break;
      case Accessor.TypeEnum.MAT3:
        elemPerVec = 9;
        break;
      case Accessor.TypeEnum.MAT4:
        elemPerVec = 16;
        break;
      default:
        Logger.Error("Unknown Type!");
        throw new ArgumentException($"Unknown Type! {nameof(type)}");
    }

    return (elemPerVec, valueType);
  }

  public static Vector3 ToVector3(this float[] vec3) {
    return new Vector3(vec3[0], vec3[1], vec3[2]);
  }
  public static Vector3 ToVector3(this Vector4 vector4) {
    return new Vector3(vector4.X, vector4.Y, vector4.Z);
  }
  public static Vector3[] ToVector3Array(this float[][] vec3Array) {
    return vec3Array.Select(x => x.ToVector3()).ToArray();
  }

  public static Vector2 ToVector2(this float[] vec2) {
    return new Vector2(vec2[0], vec2[1]);
  }
  public static Vector2[] ToVector2Array(this float[][] vec2Array) {
    return vec2Array.Select(x => x.ToVector2()).ToArray();
  }

  public static Vector4 ToVector4(this float[] vec4) {
    if (vec4.Length < 4) {
      var returnVec = new Vector4();
      for (int i = 0; i < vec4.Length; i++) {
        returnVec[i] = vec4[i];
      }
      for (int i = vec4.Length; i < 4; i++) {
        returnVec[i] = 0;
      }
      return returnVec;
    } else {
      return new Vector4(vec4[0], vec4[1], vec4[2], vec4[3]);
    }
  }
  public static Vector4[] ToVector4Array(this float[][] vec4Array) {
    return [.. vec4Array.Select(x => x.ToVector4())];
  }

  public static float[] ToFloatArray(this float[][] floatArray) {
    return [.. floatArray.SelectMany(x => x)];
  }

  public static Vector4I ToVec4I(this ushort[] batch) {
    return new Vector4I(batch[0], batch[1], batch[2], batch[3]);
  }
  public static Vector4I ToVec4I(this byte[] batch) {
    return new Vector4I(batch[0], batch[1], batch[2], batch[3]);
  }
  public static Vector4I[] ToVec4IArray(this ushort[][] ushorts) {
    return [.. ushorts.Select(x => x.ToVec4I())];
  }
  public static Vector4I[] ToVec4IArray(this byte[][] ushorts) {
    return [.. ushorts.Select(x => x.ToVec4I())];
  }

  public static Matrix4x4 ToMatrix4x4(this float[] floats) {
    var std = new Matrix4x4(
      floats[0], floats[1], floats[2], floats[3],
      floats[4], floats[5], floats[6], floats[7],
      floats[8], floats[9], floats[10], floats[11],
      floats[12], floats[13], floats[14], floats[15]
    );

    return std;
  }
  public static Matrix4x4[] ToMatrix4x4Array(this float[][] floats) {
    return [.. floats.Select(x => x.ToMatrix4x4())];
  }

  public static Quaternion ToQuat(this float[] floats) {
    return new Quaternion(floats[0], floats[1], floats[2], floats[3]);
  }
}