using System.Numerics;
using System.Runtime.InteropServices;

namespace Dwarf.EntityComponentSystem;

/*
[StructLayout(LayoutKind.Explicit)]
public struct MaterialData {
  [FieldOffset(0)] public Vector3 Color;
  [FieldOffset(16)] public Vector3 Ambient;
  [FieldOffset(32)] public Vector3 Diffuse;
  [FieldOffset(48)] public Vector3 Specular;
  [FieldOffset(60)] public float Shininess;
}
*/

[StructLayout(LayoutKind.Sequential)]
public struct MaterialData {
  public Vector3 Color;
  public Vector3 Ambient;
  public Vector3 Diffuse;
  public Vector3 Specular;
  public float Shininess;
}

public struct TexCoordSets {
  public uint BaseColor;
  public uint MetallicRoughness;
  public uint SpecularGlossiness;
  public uint Normal;
  public uint Occlusion;
  public uint Emissive;
}

public enum AlphaMode {
  Opaque,
  Mask,
  Blend
}

public class MaterialComponent {
  private MaterialData _materialData;

  internal Guid EntityId { get; init; }

  public MaterialComponent(Guid entityId) {
    EntityId = entityId;
    _materialData = new() {
      Color = new(1, 1, 1),
      Shininess = 0.001f,
      Ambient = new(1.0f, 1.0f, 1.0f),
      Diffuse = new(0.5f, 0.5f, 0.5f),
      Specular = new(1, 1, 1)
    };
  }

  public MaterialComponent(Guid entityId, Vector3 color) {
    EntityId = entityId;
    _materialData = new() {
      Color = color,
      Shininess = 0.001f,
      Ambient = new(1.0f, 1.0f, 1.0f),
      Diffuse = new(0.5f, 0.5f, 0.5f),
      Specular = new(1, 1, 1)
    };
  }

  public MaterialComponent(MaterialData materialData) {
    _materialData = materialData;
  }

  public Vector3 Color {
    get { return _materialData.Color; }
    set { _materialData.Color = value; }
  }

  public Vector3 Ambient {
    get { return _materialData.Ambient; }
    set { _materialData.Ambient = value; }
  }

  public Vector3 Diffuse {
    get { return _materialData.Diffuse; }
    set { _materialData.Diffuse = value; }
  }

  public Vector3 Specular {
    get { return _materialData.Specular; }
    set { _materialData.Specular = value; }
  }

  public float Shininess {
    get { return _materialData.Shininess; }
    set { _materialData.Shininess = value; }
  }

  public MaterialData Data => _materialData;
}

public class Material(string name = Material.NO_MATERIAL) {
  const string NO_MATERIAL = "no_material";
  public string Name { get; init; } = name;

  public int BaseColorTextureIndex { get; set; }
  public int MetallicRoughnessTextureIndex { get; set; }
  public int NormalTextureIndex { get; set; }
  public int OcclusionTextureIndex { get; set; }
  public int EmissiveTextureIndex { get; set; }

  public AlphaMode AlphaMode = AlphaMode.Opaque;
  public TexCoordSets TexCoordSets { get; set; }
  public bool DoubleSided { get; set; }

  public float AlphaCutoff = 1.0f;
  public float MetallicFactor = 1.0f;
  public float RoughnessFactor = 1.0f;
}