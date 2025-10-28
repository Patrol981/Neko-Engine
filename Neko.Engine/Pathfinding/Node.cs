using System.Numerics;

using Neko.Math;
using Neko.Utils;

namespace Neko.Pathfinding.AStar;

public class Node : IHeapItem<Node> {
  public bool Walkable { get; private set; }
  public Vector3 WorldPosition { get; private set; }
  public Vector2I GridPosition { get; private set; }
  public int GCost { get; set; }
  public int HCost { get; set; }
  public Node Parent { get; set; }

  public Node(bool walkable, Vector3 worldPosition, int x, int y, Node parent = null!) {
    Walkable = walkable;
    WorldPosition = worldPosition;
    GridPosition = new(x, y);
    Parent = parent;
  }

  public int FCost => GCost + HCost;

  public int HeapIndex { get; set; }

  public int CompareTo(Node? other) {
    var compare = FCost.CompareTo(other?.FCost);
    if (compare == 0) {
      compare = HCost.CompareTo(other?.HCost);
    }
    return -compare;
  }
}