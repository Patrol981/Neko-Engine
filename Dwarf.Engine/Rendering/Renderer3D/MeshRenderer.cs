using System.Numerics;

using Dwarf.AbstractionLayer;
using Dwarf.EntityComponentSystem;
using Dwarf.Loaders;
using Dwarf.Math;
using Dwarf.Physics;
using Dwarf.Rendering.Renderer3D.Animations;
using Dwarf.Vulkan;

using Vortice.Vulkan;

namespace Dwarf.Rendering.Renderer3D;

public class MeshRenderer : IRender3DElement, ICollision {
  private readonly IDevice _device = null!;
  private readonly IRenderer _renderer = null!;
  private readonly AABB _mergedAABB = new();

  private VkDescriptorSet _skinDescriptor = VkDescriptorSet.Null;

  public Entity Owner { get; internal set; }

  public MeshRenderer(Entity owner) {
    Owner = owner;
  }

  public MeshRenderer(Entity owner, IDevice device, IRenderer renderer) {
    Owner = owner;
    _device = device;
    _renderer = renderer;
  }

  public MeshRenderer(
    Entity owner,
    IDevice device,
    IRenderer renderer,
    Node[] nodes,
    Node[] linearNodes
  ) {
    Owner = owner;
    _device = device;
    _renderer = renderer;
    Init(nodes, linearNodes);
  }

  public MeshRenderer(
    Entity owner,
    IDevice device,
    IRenderer renderer,
    Node[] nodes,
    Node[] linearNodes,
    string fileName
  ) {
    Owner = owner;
    _device = device;
    _renderer = renderer;
    FileName = fileName;
    Init(nodes, linearNodes);
  }

  public void Init(AABBFilter aabbFilter = AABBFilter.None) {
    NodesCount = Nodes.Length;
    MeshedNodesCount = LinearNodes.Where(x => x.HasMesh).Count();
    LinearNodesCount = LinearNodes.Length;

    MeshedNodes = LinearNodes.Where(x => x.HasMesh).ToArray();

    InitBase(aabbFilter);
  }

  protected void Init(Node[] nodes, Node[] linearNodes, AABBFilter aabbFilter = AABBFilter.None) {
    NodesCount = nodes.Length;
    MeshedNodesCount = linearNodes.Where(x => x.HasMesh).Count();
    LinearNodesCount = linearNodes.Length;

    Nodes = nodes;
    LinearNodes = linearNodes;
    MeshedNodes = LinearNodes.Where(x => x.HasMesh).ToArray();

    InitBase(aabbFilter);
  }

  private void InitBase(AABBFilter aabbFilter = AABBFilter.None) {
    AABBFilter = aabbFilter;
    AABBArray = new AABB[MeshedNodesCount];

    List<Task> createTasks = [];

    if (LinearNodesCount < 1) throw new ArgumentOutOfRangeException(nameof(LinearNodesCount));

    for (int i = 0; i < MeshedNodes.Length; i++) {
      if (MeshedNodes[i].HasMesh) {
        createTasks.Add(MeshedNodes[i].Mesh!.CreateVertexBuffer());
        createTasks.Add(MeshedNodes[i].Mesh!.CreateIndexBuffer());

        AABBArray[i] = new();
        AABBArray[i].Update(MeshedNodes[i].Mesh!);

        _mergedAABB.GetBounds(MeshedNodes[i].GetMatrix());
      }
    }

    var bb = new BoundingBox(float.MaxValue, float.MinValue);
    for (int i = 0; i < MeshedNodesCount; i++) {
      CalculateBoundingBox(ref MeshedNodes[i], ref bb);
    }

    var x = MathF.Abs(MathF.Abs(bb.Min.X) + MathF.Abs(bb.Max.X));
    var y = MathF.Abs(MathF.Abs(bb.Min.Y) + MathF.Abs(bb.Max.Y));
    if (x > y) {
      Radius = x;
    } else {
      Radius = y;
    }
    RunTasks(createTasks);
  }

  public async void AddModelToTargetNode(string path, int idx, NodeInfo overrideInfo) {
    var modelToAdd = await GLTFLoaderKHR.LoadGLTF(Owner, Application.Instance, path);
    var target = NodeFromIndex(idx);

    var newLinear = LinearNodes.ToList();
    var toCopy = modelToAdd.LinearNodes.ToList();
    foreach (var node in toCopy) {
      AddLinearNode(node);
      AddNode(node, idx);
      AddedNodes.Add(node, target!);

      node.Translation = target!.Translation;
      node.Rotation = target!.Rotation;
      node.Scale = target!.Scale;

      node.TranslationOffset = overrideInfo.Translation;
      node.RotationOffset =
        Quaternion.Identity *
        new Quaternion(overrideInfo.Rotation.X, overrideInfo.Rotation.Y, overrideInfo.Rotation.Z, overrideInfo.Rotation.W);
      node.ScaleOffset = overrideInfo.Scale;

      node.NodeMatrix = target!.NodeMatrix;
      node.Update();
    }

    MeshedNodes = LinearNodes.Where(x => x.HasMesh).ToArray();

    Application.Instance.AddModelToReloadQueue(this);
    Application.Instance.Systems.Reload3DRenderSystem = true;
  }

  public void EnableNode(Func<Node, bool> predicate, bool enabled) {
    var nodes = MeshedNodes.Where(predicate);
    foreach (var node in nodes) {
      node.Enabled = enabled;
    }
  }

  public unsafe ulong CalculateBufferSize() {
    ulong baseSize = (ulong)sizeof(Matrix4x4);
    ulong currentBufferSize = 0;
    foreach (var node in MeshedNodes) {
      if (node.HasSkin) {
        currentBufferSize += ((ulong)node.Skin!.OutputNodeMatrices.Length * baseSize);
      }
    }
    return currentBufferSize;
  }

  protected async void RunTasks(List<Task> createTasks) {
    await Task.WhenAll(createTasks);
    FinishedInitialization = true;
  }

  public Task Bind(IntPtr commandBuffer, uint index) {
    var node = LinearNodes[index];
    if (!node.HasMesh) return Task.CompletedTask;

    _renderer.CommandList.BindVertex(commandBuffer, node.Mesh!.VertexBuffer!, 0);

    if (node.Mesh!.HasIndexBuffer) {
      _renderer.CommandList.BindIndex(commandBuffer, node.Mesh!.IndexBuffer!);
    }

    return Task.CompletedTask;
  }

  public Task Draw(IntPtr commandBuffer, uint index, uint firstInstance = 0) {
    var node = LinearNodes[index];
    if (!node.HasMesh) return Task.CompletedTask;

    if (node.Mesh!.HasIndexBuffer) {
      _renderer.CommandList.DrawIndexed(commandBuffer, node.Mesh!.IndexCount, 1, 0, 0, firstInstance);
    } else {
      _renderer.CommandList.Draw(commandBuffer, node.Mesh!.VertexCount, 1, 0, firstInstance);
    }
    return Task.CompletedTask;
  }

  public void BindToTextureMaterial(
    TextureManager textureManager,
    in List<(Guid id, ITexture texture)> inputTextures,
    int modelPart = 0
  ) {
    var material = MeshedNodes[modelPart]?.Mesh?.Material;
    // var targetTexture = textureManager.GetTexture(material!.BaseColorTextureIndex);
    var targetTexture = inputTextures.Where(x => x.texture?.TextureIndex == material?.BaseColorTextureIndex).SingleOrDefault();
    if (targetTexture.texture == null) return;
    MeshedNodes[modelPart]?.Mesh?.BindToTexture(textureManager, targetTexture.id);
  }

  public void BindToTexture(
    TextureManager textureManager,
    string texturePath,
    int modelPart = 0
  ) {
    MeshedNodes[modelPart]?.Mesh?.BindToTexture(textureManager, texturePath);
  }

  public void BindToTexture(TextureManager textureManager, Guid textureId, int modelPart = 0) {
    MeshedNodes[modelPart]?.Mesh?.BindToTexture(textureManager, textureId);
  }

  public void BindMultipleModelPartsToTexture(TextureManager textureManager, string path) {
    for (int i = 0; i < MeshedNodesCount; i++) {
      BindToTexture(textureManager, path, i);
    }
  }

  public void BindMultipleModelPartsToTextures(
    TextureManager textureManager,
    ReadOnlySpan<string> paths
  ) {
    for (int i = 0; i < LinearNodesCount; i++) {
      BindToTexture(textureManager, paths[i], i);
    }
  }

  public void BuildDescriptors(IDescriptorSetLayout descriptorSetLayout, IDescriptorPool descriptorPool) {
    unsafe {
      var range = Ssbo.GetDescriptorBufferInfo(Ssbo.GetAlignmentSize());
      range.range = Ssbo.GetAlignmentSize();

      _ = new VulkanDescriptorWriter((VulkanDescriptorSetLayout)descriptorSetLayout, (VulkanDescriptorPool)descriptorPool)
      .WriteBuffer(0, &range)
      .Build(out _skinDescriptor);
    }
  }

  public void CalculateBoundingBox(ref Node meshNode, ref BoundingBox boundingBox) {
    var bb = BoundingBox.GetBoundingBox(meshNode.Mesh?.Vertices);

    if (bb.HasValue) {
      meshNode.BoundingVolume = bb.Value;
      var x = MathF.Abs(MathF.Abs(bb.Value.Min.X) + MathF.Abs(bb.Value.Max.X));
      var y = MathF.Abs(MathF.Abs(bb.Value.Min.Y) + MathF.Abs(bb.Value.Max.Y));
      if (x > y) {
        meshNode.Radius = x / 2;
      } else {
        meshNode.Radius = y / 2;
      }
      meshNode.CalculateMeshCenter();
      boundingBox.Min = Vector3.Min(boundingBox.Min, bb.Value.Min);
      boundingBox.Max = Vector3.Max(boundingBox.Max, bb.Value.Max);
    }
  }

  public void CalculateBoundingBox(Node node, Node parent) {
    BoundingBox parentBB = parent != null ? parent.BoundingVolume : new BoundingBox(float.MaxValue, -float.MaxValue);

    if (node.HasMesh) {
      if (!node.Mesh!.BoundingBox.IsValid) {
        node.AABB = node.Mesh!.BoundingBox.GetBoundingBox(node.GetMatrix());
        if (node.Children?.Count > 0) {
          node.BoundingVolume.Min = node.AABB.Min;
          node.BoundingVolume.Max = node.AABB.Max;
          node.BoundingVolume.IsValid = true;
        }
      }
    }

    parentBB.Min = Vector3.Min(parentBB.Min, node.BoundingVolume.Min);
    parentBB.Max = Vector3.Max(parentBB.Max, node.BoundingVolume.Max);
    node.BoundingVolume = parentBB;

    if (node.Children?.Count < 1) return;

    foreach (var child in node.Children!) {
      CalculateBoundingBox(child, node);
    }
  }

  private void HandleCopyNode(Node node, ref Node[] linearNodes, ref int index) {
    linearNodes[index] = node;
    index++;
    if (node.Children?.Count > 0) {
      foreach (var child in node.Children) {
        HandleCopyNode(child, ref linearNodes, ref index);
      }
    }
  }

  public void CopyTo(ref MeshRenderer otherMeshRenderer) {
    otherMeshRenderer.NodesCount = NodesCount;
    otherMeshRenderer.MeshedNodesCount = MeshedNodesCount;
    otherMeshRenderer.LinearNodesCount = LinearNodesCount;

    otherMeshRenderer.Nodes = new Node[NodesCount];
    otherMeshRenderer.MeshedNodes = new Node[MeshedNodesCount];
    otherMeshRenderer.LinearNodes = new Node[LinearNodesCount];

    otherMeshRenderer.Nodes = Nodes.Select(x => (Node)x.Clone()).ToArray();
    otherMeshRenderer.Skins = Skins.Select(x => (Skin)x.Clone()).ToList();
    var tmpLinear = new Node[LinearNodesCount];
    // otherMeshRenderer.LinearNodes = LinearNodes.Select(x => (Node)x.Clone()).ToArray();
    // otherMeshRenderer.MeshedNodes = MeshedNodes.Select(x => (Node)x.Clone()).ToArray();

    int index = 0;
    foreach (var node in otherMeshRenderer.Nodes) {
      HandleCopyNode(node, ref tmpLinear, ref index);
    }
    otherMeshRenderer.LinearNodes = tmpLinear;
    if (otherMeshRenderer.LinearNodes != null) {
      otherMeshRenderer.MeshedNodes = otherMeshRenderer.LinearNodes.Where(x => x.HasMesh).ToArray();
    }

    foreach (var node in otherMeshRenderer.Nodes) {
      node.ParentRenderer = otherMeshRenderer;
    }
    if (otherMeshRenderer.LinearNodes != null) {
      foreach (var node in otherMeshRenderer.LinearNodes) {
        node.ParentRenderer = otherMeshRenderer;
        if (node.HasSkin) {
          node.Skin = otherMeshRenderer.Skins[node.SkinIndex];
          node.Update();
        }
      }
      foreach (var node in otherMeshRenderer.MeshedNodes) {
        node.ParentRenderer = otherMeshRenderer;
      }
    }

    otherMeshRenderer.Animations = Animations.Select(x => (Animation)x.Clone()).ToList();
    // otherMeshRenderer.Skins = [.. Skins];
    // otherMeshRenderer.Ssbo = Ssbo;

    otherMeshRenderer.InverseMatrices = new Matrix4x4[InverseMatrices.Length];
    // InverseMatrices.CopyTo(otherMeshRenderer.InverseMatrices, 0);

    otherMeshRenderer.FileName = FileName;
    otherMeshRenderer.TextureFlipped = TextureFlipped;

    otherMeshRenderer.InitBase();
  }

  public unsafe void Dispose() {
    foreach (var node in LinearNodes) {
      _device.WaitQueue();
      _device.WaitDevice();
      node.Dispose();
    }

    Ssbo?.Dispose();
  }

  public int NodesCount { get; private set; } = 0;
  public int MeshedNodesCount { get; private set; } = 0;
  public int LinearNodesCount { get; private set; } = 0;
  public Node[] Nodes { get; private set; } = [];
  public Node[] LinearNodes { get; private set; } = [];
  public Node[] MeshedNodes { get; private set; } = [];
  /// <summary>
  /// Node Key = added node
  /// Node Value = referenced node
  /// </summary>
  public Dictionary<Node, Node> AddedNodes { get; private set; } = [];

  public List<Animation> Animations = [];
  public List<Skin> Skins = [];

  public DwarfBuffer Ssbo { get; set; } = null!;
  public Matrix4x4[] InverseMatrices { get; set; } = [];


  public VkDescriptorSet SkinDescriptor => _skinDescriptor;
  public string FileName { get; private set; } = "";
  public int TextureFlipped { get; set; } = 1;
  public void AddNode(Node node) {
    node.ParentRenderer = this;
    var tmp = Nodes.ToList();
    tmp.Add(node);
    Nodes = [.. tmp];
  }
  public void AddNode(Node node, int parentIdx) {
    node.ParentRenderer = this;
    var targetParent = NodeFromIndex(parentIdx);
    targetParent?.Children.Add(node);
  }
  public void AddLinearNode(Node node) {
    node.ParentRenderer = this;
    var tmp = LinearNodes.ToList();
    tmp.Add(node);
    LinearNodes = [.. tmp];
  }
  public void AddJoint(Node[] jointsToAdd, int nodeIdx, int jointIdx) {
    // node.ParentRenderer = this;
    var targetNode = NodeFromIndex(nodeIdx);
    var joints = targetNode?.Skin?.Joints;
    // targetNode.Skin.Joints.Where(x => x.Index == jointIdx).First().
  }

  public Node? FindNode(Node parent, int idx) {
    Node? found = null!;
    if (parent.Index == idx) {
      return parent;
    }
    foreach (var child in parent.Children) {
      found = FindNode(child, idx);
      if (found != null) {
        break;
      }
    }
    return found;
  }
  public Node? NodeFromIndex(int idx) {
    Node? found = null!;
    foreach (var node in Nodes) {
      found = FindNode(node, idx);
      if (found != null) {
        break;
      }
    }
    return found;
  }
  public float CalculateHeightOfAnModel() {
    var height = 0.0f;
    foreach (var n in LinearNodes) {
      height += n.Mesh?.Height != null ? n.Mesh.Height : 0;
    }
    return height;
  }
  public Guid GetTextureIdReference(int index = 0) {
    return MeshedNodes[index].Mesh != null ? MeshedNodes[index].Mesh!.TextureIdReference : Guid.Empty;
  }
  public bool FinishedInitialization { get; private set; } = false;

  public bool IsSkinned {
    get {
      // return LinearNodes.Where(x => x.Skin != null).Count() > 0;
      return MeshedNodes.Where(x => x.SkinIndex > -1).Count() > 0;
    }
  }

  public bool FilterMeInShader { get; set; }

  public IRenderer Renderer => _renderer;
  public AABB[] AABBArray { get; private set; } = [];

  public AABBFilter AABBFilter { get; set; } = AABBFilter.Default;
  public AABB AABB {
    // get {
    //   return Owner!.HasComponent<ColliderMesh>()
    //     ? AABB.CalculateOnFlyWithMatrix(Owner!.GetComponent<ColliderMesh>().Mesh, Owner!.GetComponent<Transform>())
    //     : _mergedAABB;
    // }
    // get => _mergedAABB;
    get {
      _mergedAABB.CalculateOnFly(
        colliderMesh: Owner.GetColliderMesh()!,
        transform: Owner.GetTransform()!
      );
      return _mergedAABB;
    }
  }
  public float Radius { get; private set; }

  List<Animation> IRender3DElement.Animations => Animations;
}