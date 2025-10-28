using System.Numerics;
using System.Text.Json.Serialization;
using Neko.AbstractionLayer;
using Neko.Math;
using Neko.Rendering;
using Neko.Rendering.Renderer3D;
using Neko.Rendering.Renderer3D.Animations;

namespace Neko.Loaders;

public class FileVertex {
  public FileVector3? Position { get; set; }
  public FileVector3? Color { get; set; }
  public FileVector3? Normal { get; set; }
  public FileVector2? Uv { get; set; }

  public FileVector4I? JointIndices { get; set; }
  public FileVector4? JointWeights { get; set; }

  public static FileVertex ToFileVertex(Vertex vertex) {
    return new FileVertex {
      Position = FileVector3.GetFileVector3(vertex.Position),
      Color = FileVector3.GetFileVector3(vertex.Color),
      Normal = FileVector3.GetFileVector3(vertex.Normal),
      Uv = FileVector2.GetFileVector2(vertex.Uv),

      JointIndices = FileVector4I.GetFileVector4I(vertex.JointIndices),
      JointWeights = FileVector4.GetFileVector4(vertex.JointWeights),
    };
  }

  public static Vertex FromFileVertex(FileVertex vertex) {
    return new Vertex {
      Position = vertex.Position?.Values.Length > 0 ? FileVector3.ParseFileVector3(vertex.Position!) : Vector3.Zero,
      Color = vertex.Color?.Values.Length > 0 ? FileVector3.ParseFileVector3(vertex.Color!) : Vector3.Zero,
      Normal = vertex.Normal?.Values.Length > 0 ? FileVector3.ParseFileVector3(vertex.Normal!) : Vector3.Zero,
      Uv = vertex.Uv?.Values.Length > 0 ? FileVector2.ParseFileVector2(vertex.Uv!) : Vector2.Zero,

      JointIndices = vertex.JointIndices?.Values.Length > 0 ? FileVector4I.ParseFileVector4I(vertex.JointIndices) : new(0, 0, 0, 0),
      JointWeights = vertex.JointWeights?.Values.Length > 0 ? FileVector4.ParseFileVector4(vertex.JointWeights) : Vector4.Zero
    };
  }

  public static List<FileVertex> GetFileVertices(Vertex[] vertices) {
    return vertices.Select(x => FileVertex.ToFileVertex(x)).ToList();
  }

  public static Vertex[] FromFileVertices(List<FileVertex> fileVertices) {
    return fileVertices.Select(x => FileVertex.FromFileVertex(x)).ToArray();
  }
}

public class FileMesh {
  public List<FileVertex>? Vertices { get; set; }
  public List<uint>? Indices { get; set; }
  public ulong VertexCount { get; set; }
  public ulong IndexCount { get; set; }

  [JsonIgnore] public ITexture? Texture { get; set; }

  public string? BinaryReferenceName { get; set; }
  public ulong BinaryOffset { get; set; }
  public ulong BinaryTextureSize { get; set; }
  public string TextureFileName { get; set; } = string.Empty;
  public int TextureWidth { get; set; }
  public int TextureHeight { get; set; }

  public static byte[] GetTextureDataOutOfId(TextureManager textureManager, Guid texId) {
    if (texId == Guid.Empty) return null!;

    var tex = textureManager.GetTextureLocal(texId);
    return tex.TextureData;
  }

  public static ITexture GetTextureOutOfId(TextureManager textureManager, Guid texId) {
    if (texId == Guid.Empty) return null!;

    return textureManager.GetTextureLocal(texId);
  }

  public static Guid CreateTextureFromFile(string fileName) {
    return Guid.Empty;
  }

  public static FileMesh ToFileMesh(Mesh mesh) {
    return new FileMesh {
      Vertices = FileVertex.GetFileVertices(mesh.Vertices),
      Indices = [.. mesh.Indices],
      VertexCount = mesh.VertexCount,
      IndexCount = mesh.IndexCount,
      Texture = GetTextureOutOfId(Application.Instance.TextureManager, mesh.TextureIdReference)
    };
  }

  public static Mesh FromFileMesh(FileMesh fileMesh) {
    var app = Application.Instance;
    var mesh = new Mesh(app.Allocator, app.Device) {
      Vertices = fileMesh.Vertices?.Count > 0 ? FileVertex.FromFileVertices(fileMesh.Vertices) : null!,
      Indices = fileMesh.Indices?.Count > 0 ? [.. fileMesh.Indices] : null!,
    };

    return mesh;
  }
}

public class FileNode {
  public int Index { get; set; }
  public int ParentIndex { get; set; } = -1;
  public List<FileNode>? Children { get; set; }
  public string Name { get; set; } = string.Empty;
  public FileMesh? Mesh { get; set; }
  public FileSkin? Skin { get; set; }
  public int SkinIndex { get; set; }
  public FileVector3? Translation { get; set; }
  public FileQuaternion? Rotation { get; set; }
  public FileVector3? Scale { get; set; }

  public static FileNode ToFileNode(Node node) {
    if (node == null) return null!;

    var fileNode = new FileNode {
      Index = node.Index,
      SkinIndex = node.SkinIndex,
      Translation = FileVector3.GetFileVector3(node.Translation),
      Rotation = FileQuaternion.GetFileQuaternion(node.Rotation),
      Scale = FileVector3.GetFileVector3(node.Scale),
      Name = node.Name,
      ParentIndex = node.Parent != null ? node.Parent.Index : -1,
    };

    if (node.Children != null && node.Children.Count != 0) {
      fileNode.Children = [];
      foreach (var childNode in node.Children) {
        fileNode.Children!.Add(ToFileNode(childNode));
      }
    }

    // if (node.HasSkin) {
    //   fileNode.Skin = FileSkin.ToFileSkin(node.Skin!);
    // }

    if (node.HasMesh) {
      // fileNode.Mesh = FileMesh.ToFileMesh(node.Mesh!);
    }

    return fileNode;
  }

  public static Node FromFileNode(FileNode fileNode, Node parent = null!) {
    if (fileNode == null) return null!;

    var node = new Node(Application.Instance, default) {
      Index = fileNode.Index,
      SkinIndex = fileNode.SkinIndex,
      Translation = fileNode.Translation != null ? FileVector3.ParseFileVector3(fileNode.Translation) : Vector3.Zero,
      Rotation = fileNode.Rotation != null ? FileQuaternion.ParseQuaternion(fileNode.Rotation) : Quaternion.Identity,
      Scale = fileNode.Scale != null ? FileVector3.ParseFileVector3(fileNode.Scale) : Vector3.One,
      Name = fileNode.Name,
    };

    if (parent != null) {
      node.Parent = parent;
    }

    if (fileNode.Mesh != null) {
      // node.Mesh = FileMesh.FromFileMesh(fileNode.Mesh!);
    }

    return node;
  }

  public static List<FileNode> ToFileNodes(List<Node> nodes) {
    List<FileNode> fileNodes = [];

    foreach (Node node in nodes) {
      fileNodes.Add(ToFileNode(node));
    }

    return fileNodes;
  }

  public static List<Node> FromFileNodes(List<FileNode> fileNodes) {
    List<Node> nodes = [];

    foreach (var fileNode in fileNodes) {
      nodes.Add(FromFileNode(fileNode));
    }

    return nodes;
  }
}

public class FileSkin {
  public string Name { get; set; } = default!;
  public int SkeletonRoot { get; set; }
  public List<FileMatrix4x4>? InverseBindMatrices { get; set; }
  public List<int> JointIndices { get; set; } = [];
  public List<FileMatrix4x4>? OutputNodeMatrices { get; set; }
  public int JointsCount { get; set; }

  public static FileSkin ToFileSkin(Skin skin) {
    return new FileSkin {
      Name = skin.Name,
      SkeletonRoot = skin.SkeletonRoot != null ? skin.SkeletonRoot.Index : -1,
      InverseBindMatrices = FileMatrix4x4.GetFileMatrices(skin.InverseBindMatrices),
      JointIndices = skin.Joints.Select(x => x.Index).ToList(),
      OutputNodeMatrices = FileMatrix4x4.GetFileMatrices([.. skin.OutputNodeMatrices]),
      JointsCount = skin.JointsCount
    };
  }

  public static Skin FromFileSkin(FileSkin fileSkin) {
    return new Skin {
      Name = fileSkin.Name,
      InverseBindMatrices = fileSkin.InverseBindMatrices != null ? FileMatrix4x4.FromFileMatrices(fileSkin.InverseBindMatrices) : null!,
      OutputNodeMatrices = fileSkin.JointsCount > 0 ? [.. FileMatrix4x4.FromFileMatrices(fileSkin.OutputNodeMatrices!)] : null!,
      JointsCount = fileSkin.JointsCount
    };
  }

  public static List<FileSkin> ToFileSkins(List<Skin> skins) {
    return [.. skins.Select(skin => ToFileSkin(skin))];
  }

  public static List<Skin> FromFileSkins(List<FileSkin> fileSkins) {
    return [.. fileSkins.Select(skin => FromFileSkin(skin))];
  }
}

public class FileMatrix4x4 {
  public float[] Values { get; set; } = [];

  public static FileMatrix4x4 GetFileMatrix4x4(Matrix4x4 matrix4) {
    return new FileMatrix4x4 {
      Values = [
        matrix4.M11, matrix4.M12, matrix4.M13, matrix4.M14,
        matrix4.M21, matrix4.M22, matrix4.M23, matrix4.M24,
        matrix4.M31, matrix4.M32, matrix4.M33, matrix4.M34,
        matrix4.M41, matrix4.M42, matrix4.M43, matrix4.M44,
      ]
    };
  }

  public static Matrix4x4 FromFileMatrix4x4(FileMatrix4x4 matrix4) {
    return new Matrix4x4(
      matrix4.Values[0], matrix4.Values[1], matrix4.Values[2], matrix4.Values[3],
      matrix4.Values[4], matrix4.Values[5], matrix4.Values[6], matrix4.Values[7],
      matrix4.Values[8], matrix4.Values[9], matrix4.Values[10], matrix4.Values[11],
      matrix4.Values[12], matrix4.Values[13], matrix4.Values[14], matrix4.Values[15]
    );
  }

  public static List<FileMatrix4x4> GetFileMatrices(List<Matrix4x4> matrices) {
    return [.. matrices.Select(mat => { return GetFileMatrix4x4(mat); })];
  }

  public static List<Matrix4x4> FromFileMatrices(List<FileMatrix4x4> matrices) {
    return [.. matrices.Select(mat => { return FromFileMatrix4x4(mat); })];
    // return matrices.Select(mat => { return Matrix4x4.Identity; }).ToList();
  }
}

public class FileVector3 {
  public float[] Values { get; set; } = [];

  public static FileVector3 GetFileVector3(Vector3 vector3) {
    return new FileVector3 {
      Values = [vector3.X, vector3.Y, vector3.Z]
    };
  }

  public static Vector3 ParseFileVector3(FileVector3 fileVector3) {
    return new Vector3 {
      X = fileVector3.Values[0],
      Y = fileVector3.Values[1],
      Z = fileVector3.Values[2],
    };
  }
}

public class FileVector2 {
  public float[] Values { get; set; } = [];

  public static FileVector2 GetFileVector2(Vector2 vector2) {
    return new FileVector2 {
      Values = [vector2.X, vector2.Y]
    };
  }

  public static Vector2 ParseFileVector2(FileVector2 fileVector2) {
    return new Vector2 {
      X = fileVector2.Values[0],
      Y = fileVector2.Values[1],
    };
  }
}

public class FileVector4 {
  public float[] Values { get; set; } = [];

  public static FileVector4 GetFileVector4(Vector4 vector4) {
    return new FileVector4 {
      Values = [vector4.X, vector4.Y, vector4.Z, vector4.W]
    };
  }

  public static List<FileVector4> GetFileVectors4(List<Vector4> vector4s) {
    return [.. vector4s.Select(x => { return GetFileVector4(x); })];
  }

  public static Vector4 ParseFileVector4(FileVector4 fileVector4) {
    return new Vector4 {
      X = fileVector4.Values[0],
      Y = fileVector4.Values[1],
      Z = fileVector4.Values[2],
      W = fileVector4.Values[3],
    };
  }

  public static List<Vector4> ParseFileVectors4(List<FileVector4> fileVector4s) {
    return fileVector4s.Select(x => { return ParseFileVector4(x); }).ToList();
  }
}

public class FileVector4I {
  public int[] Values { get; set; } = [];

  public static FileVector4I GetFileVector4I(Vector4I vector4) {
    return new FileVector4I {
      Values = [vector4.X, vector4.Y, vector4.Z, vector4.W]
    };
  }

  public static Vector4I ParseFileVector4I(FileVector4I fileVector4) {
    return new Vector4I {
      X = fileVector4.Values[0],
      Y = fileVector4.Values[1],
      Z = fileVector4.Values[2],
      W = fileVector4.Values[3],
    };
  }
}

public class FileQuaternion {
  public float[] Values { get; set; } = [];

  public static FileQuaternion GetFileQuaternion(Quaternion quaternion) {
    return new FileQuaternion {
      Values = [quaternion.X, quaternion.Y, quaternion.Z, quaternion.W]
    };
  }

  public static Quaternion ParseQuaternion(FileQuaternion fileQuaternion) {
    return new Quaternion {
      X = fileQuaternion.Values[0],
      Y = fileQuaternion.Values[1],
      Z = fileQuaternion.Values[2],
      W = fileQuaternion.Values[3],
    };
  }
}

public class FileAnimationChannel {
  public int PathType { get; set; }
  public int NodeIndex { get; set; }
  public int SamplerIndex { get; set; }

  public static FileAnimationChannel ToFileAnimationChannel(AnimationChannel animationChannel) {
    return new FileAnimationChannel() {
      PathType = (int)animationChannel.Path,
      NodeIndex = animationChannel.Node.Index,
      SamplerIndex = animationChannel.SamplerIndex,
    };
  }

  public static List<FileAnimationChannel> ToFileAnimationChannels(List<AnimationChannel> animationChannels) {
    return [.. animationChannels.Select(x => { return ToFileAnimationChannel(x); })];
  }

  public static AnimationChannel FromFileAnimationChannel(FileAnimationChannel fileAnimationChannel) {
    return new AnimationChannel() {
      Path = (PathType)fileAnimationChannel.PathType,
      SamplerIndex = fileAnimationChannel.SamplerIndex
    };
  }

  public static List<AnimationChannel> FromFileAnimationChannels(List<FileAnimationChannel> fileAnimationChannels) {
    return fileAnimationChannels.Select(x => { return FromFileAnimationChannel(x); }).ToList();
  }
}

public class FileAnimationSampler {
  public int InterpolationType { get; set; }
  public List<float> Inputs { get; set; } = [];
  public List<FileVector4> OutputVec4 { get; set; } = [];
  public List<float> Outputs { get; set; } = [];

  public static FileAnimationSampler ToFileAnimationSampler(AnimationSampler animationSampler) {
    return new FileAnimationSampler() {
      InterpolationType = (int)animationSampler.Interpolation,
      Inputs = animationSampler.Inputs,
      OutputVec4 = FileVector4.GetFileVectors4(animationSampler.OutputsVec4),
      Outputs = animationSampler.Outputs
    };
  }

  public static List<FileAnimationSampler> ToFileAnimationSamplers(List<AnimationSampler> animationSamplers) {
    return [.. animationSamplers.Select(x => { return ToFileAnimationSampler(x); })];
  }

  public static AnimationSampler FromFileAnimationSampler(FileAnimationSampler fileAnimationSampler) {
    return new AnimationSampler() {
      Interpolation = (AnimationSampler.InterpolationType)fileAnimationSampler.InterpolationType,
      Inputs = fileAnimationSampler.Inputs,
      OutputsVec4 = FileVector4.ParseFileVectors4(fileAnimationSampler.OutputVec4),
      Outputs = fileAnimationSampler.Outputs
    };
  }

  public static List<AnimationSampler> FromFileAnimationSamplers(List<FileAnimationSampler> fileAnimationSamplers) {
    return [.. fileAnimationSamplers.Select(x => { return FromFileAnimationSampler(x); })];
  }
}

public class FileAnimation {
  public string Name { get; set; } = string.Empty;
  public List<FileAnimationSampler> Samplers { get; set; } = [];
  public List<FileAnimationChannel> Channels { get; set; } = [];
  public float Start { get; set; }
  public float End { get; set; }

  public static FileAnimation ToFileAnimation(Animation animation) {
    return new FileAnimation {
      Name = animation.Name,
      Samplers = FileAnimationSampler.ToFileAnimationSamplers(animation.Samplers),
      Channels = FileAnimationChannel.ToFileAnimationChannels(animation.Channels),
      Start = animation.Start,
      End = animation.End,
    };
  }

  public static Animation FromFileAnimation(FileAnimation animation) {
    return new Animation {
      Name = animation.Name,
      Samplers = FileAnimationSampler.FromFileAnimationSamplers(animation.Samplers),
      Channels = FileAnimationChannel.FromFileAnimationChannels(animation.Channels),
      Start = animation.Start,
      End = animation.End,
    };
  }

  public static List<FileAnimation> ToFileAnimations(List<Animation> animations) {
    return animations.Select(anim => ToFileAnimation(anim)).ToList();
  }

  public static List<Animation> FromFileAnimations(List<FileAnimation> animations) {
    return animations.Select(anim => FromFileAnimation(anim)).ToList();
  }
}

public class NekoFile {
  public string BinaryDataRef { get; set; } = string.Empty;
  public string FileName { get; set; } = string.Empty;
  public int TextureFlipped { get; set; }
  public List<FileNode>? Nodes { get; set; }
  [JsonIgnore] public List<FileNode>? LinearNodes { get; set; }
  [JsonIgnore] public List<FileNode>? MeshedNodes { get; set; }
  public List<FileAnimation>? Animations { get; set; }
  public List<FileSkin>? Skins { get; set; }
  public List<FileMatrix4x4>? InverseMatrices { get; set; }

  public List<VulkanTexture>? Textures { get; set; }

  public static NekoFile ToNekoFile(MeshRenderer meshRenderer) {
    return new NekoFile {
      FileName = meshRenderer.FileName,
      TextureFlipped = meshRenderer.TextureFlipped,

      Nodes = FileNode.ToFileNodes([.. meshRenderer.Nodes]),
      LinearNodes = FileNode.ToFileNodes([.. meshRenderer.LinearNodes]),
      MeshedNodes = FileNode.ToFileNodes([.. meshRenderer.MeshedNodes]),

      Animations = FileAnimation.ToFileAnimations(meshRenderer.Animations),
      // Skins = FileSkin.ToFileSkins(meshRenderer.Skins),
      InverseMatrices = FileMatrix4x4.GetFileMatrices([.. meshRenderer.InverseMatrices])
    };
  }
}