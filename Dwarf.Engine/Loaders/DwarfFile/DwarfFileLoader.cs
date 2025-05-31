using System.Text;
using System.Text.Json;
using Dwarf.Rendering.Renderer3D;
using Dwarf.Vulkan;

namespace Dwarf.Loaders;

public static class DwarfFileLoader {
  public static MeshRenderer LoadMesh(Application app, string path) {
    var dwarfFile = Load(path);
    var meshRenderer = new MeshRenderer(app.Device, app.Renderer);

    // Load Textures From binary file
    var stream = new FileStream($"./Resources/{dwarfFile.BinaryDataRef}", FileMode.Open);
    var reader = new BinaryReader(stream);

    if (dwarfFile.Animations?.Count != 0) {
      meshRenderer.Animations = FileAnimation.FromFileAnimations(dwarfFile.Animations!);
    }

    if (dwarfFile.Skins?.Count != 0) {
      meshRenderer.Skins = FileSkin.FromFileSkins(dwarfFile.Skins!);
    }

    if (dwarfFile.Skins != null && dwarfFile.Skins.Count > 0) {
      for (int i = 0; i < meshRenderer.Skins.Count; i++) {
        if (dwarfFile.Skins[i].JointIndices != null && dwarfFile.Skins[i].JointIndices!.Count > 0) {
          for (int j = 0; j < dwarfFile.Skins[i]!.JointIndices!.Count; j++) {
            var target = meshRenderer.NodeFromIndex(dwarfFile.Skins[i]!.JointIndices[j]!);
            if (target != null) {
              meshRenderer.Skins[i].Joints.Add(target);
            }
          }
        }
      }
    }

    for (int i = 0; i < meshRenderer.Skins.Count; i++) {
      try {
        var node = meshRenderer.NodeFromIndex(dwarfFile.Skins![i].SkeletonRoot);
        meshRenderer.Skins[i].SkeletonRoot = node!;
      } catch {
        meshRenderer.Skins[i].SkeletonRoot = null!;
      }
    }

    if (dwarfFile.Nodes?.Count == 0) {
      throw new ArgumentException(nameof(dwarfFile.Nodes));
    }
    foreach (var node in dwarfFile.Nodes!) {
      LoadNode(null!, node, ref meshRenderer, reader, app, in dwarfFile);
    }

    foreach (var node in meshRenderer.LinearNodes) {
      if (node.SkinIndex > -1) {
        node.Skin = meshRenderer.Skins[node.SkinIndex];
        node.Skin.Init();
      }

      if (node.Mesh != null) {
        node.Update();
      }
    }

    LoadAnimations(ref meshRenderer, in dwarfFile);

    meshRenderer.Init();

    return meshRenderer;
  }

  public static DwarfFile Load(string path) {
    try {
      var file = LoadFromFile(path);

      return JsonSerializer.Deserialize<DwarfFile>(file, DwarfFileParser.ParserOptions)
      ?? throw new Exception("File is null");
    } catch {
      throw;
    }
  }

  private static string LoadFromFile(string path) {
    return File.ReadAllText(path);
  }

  private static void LoadAnimations(ref MeshRenderer meshRenderer, in DwarfFile dwarfFile) {
    for (int i = 0; i < meshRenderer.Animations.Count; i++) {
      for (int j = 0; j < meshRenderer.Animations[i].Channels.Count; j++) {
        var targetId = dwarfFile.Animations?[i].Channels[j].NodeIndex;
        if (targetId.HasValue) {
          var targetNode = meshRenderer.NodeFromIndex(targetId.Value);
          meshRenderer.Animations[i].Channels[j].Node = targetNode ?? null!;
        }
      }
    }
  }

  private static void LoadNode(
    Dwarf.Rendering.Renderer3D.Node parentNode,
    FileNode fileNode,
    ref MeshRenderer meshRenderer,
    BinaryReader reader,
    Application app,
    in DwarfFile dwarfFile
  ) {
    var newNode = FileNode.FromFileNode(fileNode, parentNode);

    if (fileNode.Mesh != null) {
      var offset = fileNode.Mesh.BinaryOffset;
      var refId = fileNode.Mesh.BinaryReferenceName;

      reader.BaseStream.Seek((long)offset + 1, SeekOrigin.Begin);

      var guidBytes = reader.ReadBytes(36);
      var guidString = Encoding.UTF8.GetString(guidBytes);
      Guid guid = Guid.Parse(guidString);

      if (guid.ToString() != fileNode.Mesh.BinaryReferenceName) {
        throw new ArgumentException("Mismatch between guid of texture.");
      }

      byte[] textureData = reader.ReadBytes((int)fileNode.Mesh.BinaryTextureSize);

      var texture = VulkanTexture.LoadFromBytesDirect(
        app.Allocator,
        (VulkanDevice)app.Device,
        textureData,
        (int)fileNode.Mesh.BinaryTextureSize,
        fileNode.Mesh.TextureWidth,
        fileNode.Mesh.TextureHeight,
        fileNode.Mesh.TextureFileName
      );

      Guid id;
      if (!app.TextureManager.TextureExistsLocal(texture)) {
        id = app.TextureManager.AddTextureLocal(texture);
      } else {
        id = app.TextureManager.GetTextureIdLocal(texture.TextureName);
        texture.Dispose();
      }

      newNode.Mesh!.BindToTexture(app.TextureManager, id);
    }

    if (fileNode.Skin != null) {
      newNode.Skin = meshRenderer.Skins[newNode.SkinIndex];
    }

    if (parentNode == null) {
      meshRenderer.AddNode(newNode);
    } else {
      parentNode.Children.Add(newNode);
    }
    meshRenderer.AddLinearNode(newNode);

    if (fileNode.Children != null && fileNode.Children?.Count != 0) {
      foreach (var childNode in fileNode.Children!) {
        LoadNode(newNode, childNode, ref meshRenderer, reader, app, in dwarfFile);
      }
    }
  }
}