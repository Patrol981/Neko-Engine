using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Neko.EntityComponentSystem;
using Neko.Utils;

namespace Neko.Physics;

public class Float3(float x, float y, float z) {
  public float X { get; set; } = x;
  public float Y { get; set; } = y;
  public float Z { get; set; } = z;
};

public class CollisionFileInfo {
  public Float3 Size { get; set; } = default!;
  public Float3 Offset { get; set; } = default!;

  public override string ToString() {
    return $"[{Size.X} {Size.Y} {Size.Z}] [{Offset.X} {Offset.Y} {Offset.Z}]";
  }
}

[JsonSerializable(typeof(Float3))]
[JsonSerializable(typeof(CollisionFileInfo))]
[JsonSerializable(typeof(List<CollisionFileInfo>))]
internal partial class CollisionFileSerializerContext : JsonSerializerContext { }

public class CollisionFile(List<CollisionFileInfo>? collisions) : IDisposable {
  public List<CollisionFileInfo> Collisions { get; private set; } = collisions ?? [];

  public static readonly JsonSerializerOptions JsonOptions = new() {
    TypeInfoResolver = CollisionFileSerializerContext.Default,
  };

  public async Task SerializeAndSave(string path) {
    await using FileStream fileStream = File.Create($"{path}.json");
    await JsonSerializer.SerializeAsync(fileStream, Collisions, JsonOptions);
  }

  public static async Task<CollisionFile?> Deserialize(string path) {
    await using FileStream fileStream = File.OpenRead(
      Path.Combine(NekoPath.AssemblyDirectory, $"{path}.json")
    );
    var fileInfo = await JsonSerializer.DeserializeAsync<List<CollisionFileInfo>>(fileStream, JsonOptions);
    return new CollisionFile(fileInfo);
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
    if (rigid.Length < 1) return [];
    var collInfo = new List<CollisionFileInfo>();
    foreach (var r in rigid) {
      if (r == null) continue;
      collInfo.Add(new() {
        Offset = new(r.Offset.X, r.Offset.Y, r.Offset.Z),
        Size = new(r.Size.X, r.Size.Y, r.Size.Z)
      });
    }
    return collInfo;
  }

  public static ReadOnlySpan<Entity> BuildRigidbodies(CollisionFile collisionFile) {
    var collBuilder = new EntityBuilder.CollisionBuilder();
    foreach (var coll in collisionFile.Collisions) {
      collBuilder.AddCollision(
        new(coll.Size.X, coll.Size.Y, coll.Size.Z),
        new(coll.Offset.X, coll.Offset.Y, coll.Offset.Z)
      );
    }
    var colls = collBuilder.Build();
    return colls;
  }

  public void Dispose() {
    Collisions.Clear();
    GC.SuppressFinalize(this);
  }
}