using System.Runtime.Serialization.Formatters.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;
using Neko.EntityComponentSystem;
using Neko.Rendering.Renderer3D;

namespace Neko.Loaders;

[JsonSerializable(typeof(NekoFile[]))]
internal partial class NekoFileJsonSerializerContext : JsonSerializerContext { }

public static class NekoFileParser {
  public readonly static JsonSerializerOptions ParserOptions = new() {
    WriteIndented = true,
    ReferenceHandler = ReferenceHandler.Preserve
  };

  public static NekoFile? Parse(in Entity entity) {
    var meshRenderer = entity.GetDrawable3D();

    if (meshRenderer == null) return null;

    return NekoFile.ToNekoFile((meshRenderer as MeshRenderer)!);
  }

  public static void SaveToFile(string path, NekoFile file) {
    // run it in a thread pool so it's not blocking the main thread
    Task.Run(() => {
      using var stream = new FileStream($"{path}.bin", FileMode.OpenOrCreate);
      using var writer = new BinaryWriter(stream);

      if (file.Nodes?.Count != 0) {
        foreach (var node in file.Nodes!) {
          HandleNode(node, in writer);
        }
      }

      var fileTargetBin = Path.GetFileName($"{path}.bin");
      file.BinaryDataRef = fileTargetBin;

      var outputString = JsonSerializer.Serialize<NekoFile>(file, ParserOptions);
      File.WriteAllText($"{path}.json", outputString);

      return Task.CompletedTask;
    });
  }

  private static void HandleNode(FileNode node, in BinaryWriter writer) {
    if (node.Mesh != null && node.Mesh.Texture != null) {
      var uid = Guid.NewGuid();
      node.Mesh.BinaryReferenceName = uid.ToString();
      node.Mesh.BinaryOffset = (ulong)writer.BaseStream.Position;
      node.Mesh.BinaryTextureSize = (ulong)node.Mesh.Texture.Size;
      node.Mesh.TextureFileName = node.Mesh.Texture.TextureName;
      node.Mesh.TextureWidth = node.Mesh.Texture.Width;
      node.Mesh.TextureHeight = node.Mesh.Texture.Height;

      writer.Write(uid.ToString());
      writer.Write(node.Mesh.Texture.TextureData);
    }

    if (node.Children != null && node.Children?.Count != 0) {
      foreach (var childNodde in node.Children!) {
        HandleNode(childNodde, in writer);
      }
    }
  }
}