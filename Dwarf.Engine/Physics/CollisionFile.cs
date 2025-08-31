using System.Numerics;
using Dwarf.EntityComponentSystem;
using Dwarf.Utils;
using YamlDotNet;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Dwarf.Physics;

public struct CollisionFileInfo {
  public Vector3 Size;
  public Vector3 Offset;
}

public class CollisionFile(List<CollisionFileInfo>? collisions) : IDisposable {
  public List<CollisionFileInfo> Collisions { get; private set; } = collisions ?? [];

  public string Serialize() {
    var serializer = new SerializerBuilder()
      .WithNamingConvention(UnderscoredNamingConvention.Instance)
      .Build();

    var yaml = serializer.Serialize(Collisions);
    return yaml;
  }

  public static CollisionFile Deserialize(string yaml) {
    var deserializer = new DeserializerBuilder()
      .WithNamingConvention(UnderscoredNamingConvention.Instance)
      .Build();

    var collisions = deserializer.Deserialize<List<CollisionFileInfo>>(yaml);
    return new CollisionFile(collisions);
  }

  /// <summary>
  /// Gets all entities that have <b> Rigidbody </b> in it and
  /// converts into <b> Collision files </b>
  /// </summary>
  /// <param name="entities"></param>
  public static List<CollisionFileInfo> FromRigidbodies(List<Entity> entities) {
    var rigid = entities
      .Where(x => x.HasComponent<Rigidbody>())
      .Select(x => x.GetRigidbody())
      .ToArray();
    var collInfo = new List<CollisionFileInfo>();
    foreach (var r in rigid) {
      collInfo.Add(new() { Offset = r.Offset, Size = r.Size });
    }
    return collInfo;
  }

  public static ReadOnlySpan<Entity> BuildRigidbodies(string yaml) {
    var collBuilder = new EntityBuilder.CollisionBuilder();
    var collInfo = Deserialize(yaml);
    foreach (var coll in collInfo.Collisions) {
      collBuilder.AddCollision(coll.Size, coll.Offset);
    }
    var colls = collBuilder.Build();
    return colls;
  }

  public static void SaveToFile(string path, string data) {
    try {
      File.WriteAllText($"{path}.yaml", data, System.Text.Encoding.UTF8);
    } catch {
      throw;
    }
  }

  public static string ReadFromFile(string path, bool readFromResources = true) {
    try {
      if (readFromResources) {
        path = Path.Combine(DwarfPath.AssemblyDirectory, path);
      }
      return File.ReadAllText(path);
    } catch {
      throw;
    }
  }

  public void Dispose() {
    Collisions.Clear();
    GC.SuppressFinalize(this);
  }
}