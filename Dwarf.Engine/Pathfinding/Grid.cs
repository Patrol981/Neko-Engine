using System.Numerics;

using Dwarf.EntityComponentSystem;
using Dwarf.Globals;
using Dwarf.Math;
using Dwarf.Rendering;
using Dwarf.Rendering.Guizmos;

namespace Dwarf.Pathfinding.AStar;

public class Grid : DwarfScript {
  public Vector2 GridSizeWorld = new(15, 15);
  public float NodeRadius = 0.25f;
  public EntityLayer UnwalkableLayer = EntityLayer.Collision;
  private float _nodeDiameter;
  private int _gridSizeX;
  private int _gridSizeY;

  public override void Awake() {
    _nodeDiameter = NodeRadius * 2;
    _gridSizeX = (int)(GridSizeWorld.X / _nodeDiameter);
    _gridSizeY = (int)(GridSizeWorld.Y / _nodeDiameter);
    CreateGrid();
  }

  public override void Update() {
    if (GridData != null) {

    }
  }

  public void PaintGuizmos() {
    foreach (var node in GridData) {
      GridGuizmos[node.GridPosition.X, node.GridPosition.Y].Color = node.Walkable ? new(0.2f, 0.7f, 0.2f) : new(1.0f, 0.0f, 0.0f);
    }
    /*
    foreach (var node in _grid) {
      GridGuizmos[node.GridPosition.X, node.GridPosition.Y].Color = node.Walkable ? new(0.2f, 0.7f, 0.2f) : new(1.0f, 0.0f, 0.0f);
      if (Path != null) {
        if (Path.Contains(node)) {
          GridGuizmos[node.GridPosition.X, node.GridPosition.Y].Color = new(1.0f, 1.0f, 0.0f);
        }
      }
    }
    */
  }

  public List<Node> GetNeighbours(Node node) {
    var neighbours = new List<Node>();

    for (int x = -1; x <= 1; x++) {
      for (int y = -1; y <= 1; y++) {
        if (x == 0 && y == 0) continue;

        var checkX = node.GridPosition.X + x;
        var checkY = node.GridPosition.Y + y;

        if (checkX >= 0 && checkX < _gridSizeX && checkY >= 0 && checkY < _gridSizeY) {
          neighbours.Add(GridData[checkX, checkY]);
        }
      }
    }

    return neighbours;
  }

  public Node NodeFromWorldPoint(Vector3 worldPos) {
    var percentX = (worldPos.X + GridSizeWorld.X / 2) / GridSizeWorld.X;
    var percentY = (worldPos.Z + GridSizeWorld.Y / 2) / GridSizeWorld.Y;
    percentX = System.Math.Clamp(percentX, 0, 1);
    percentY = System.Math.Clamp(percentY, 0, 1);

    var x = (int)System.Math.Round((_gridSizeX - 1) * percentX);
    var y = (int)System.Math.Round((_gridSizeY - 1) * percentY);

    return GridData[x, y];
  }

  internal void CreateGrid() {
    GridData = new Node[_gridSizeX, _gridSizeY];
    GridGuizmos = new Guizmo[_gridSizeX, _gridSizeY];
    var worldBottomLeft =
      Owner!.GetTransform()!.Position -
      Vector3.UnitX * GridSizeWorld.X / 2 -
      Vector3.UnitZ * GridSizeWorld.Y / 2;

    Guizmos.Clear();

    for (int x = 0; x < _gridSizeX; x++) {
      for (int y = 0; y < _gridSizeY; y++) {
        var worldPoint =
          worldBottomLeft + Vector3.UnitX * (x * _nodeDiameter + NodeRadius) +
          Vector3.UnitZ * (y * _nodeDiameter + NodeRadius);
        bool walkable = !Collision3D.CheckSphere(worldPoint, NodeRadius, UnwalkableLayer);
        GridData[x, y] = new Node(walkable, worldPoint, x, y);
        GridGuizmos[x, y] = walkable
          ? Guizmos.AddCircular(worldPoint, new(0.25f, 0.25f, 0.25f), new(0.2f, 0.7f, 0.2f))
          : Guizmos.AddCube(worldPoint, new(0.25f, 0.25f, 0.25f), new(1.0f, 0.0f, 0.0f));
      }
    }
  }

  public Guizmo[,] GridGuizmos { get; private set; } = new Guizmo[0, 0];
  public Node[,] GridData { get; private set; } = new Node[0, 0];
  public int MaxSize => _gridSizeX * _gridSizeY;
}