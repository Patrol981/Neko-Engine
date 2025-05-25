using System.Numerics;
using Dwarf.AbstractionLayer;
using Dwarf.EntityComponentSystem;
using Dwarf.Extensions.Logging;
using Dwarf.Globals;
using Dwarf.Math;
using Dwarf.Rendering;
using Dwarf.Rendering.Renderer3D;

namespace Dwarf.Procedural.Cave;

public class CaveGenerator {
  public int FillPercent { get; private set; }
  public int SmoothIterationCount { get; private set; }
  public int BorderSize { get; private set; }
  public int WallHeight { get; private set; }
  public int WallThresholdSize { get; private set; }
  public int RoomThresholdSize { get; private set; }
  public int PassageRadius { get; private set; }
  public int SquareSize { get; private set; }
  public int TileSize { get; private set; }
  public int Width { get; init; }
  public int Height { get; init; }
  public string Seed { get; init; }

  public Room? MainRoom { get; internal set; }

  public int[,] Map { get; private set; }

  public class GeneratorOptions {
    public int FillPercent = 43;
    public int SmoothIterationCount = 5;
    public int BorderSize = 5;
    public int WallHeight = 50;
    public int MapWidth = 64;
    public int MapHeight = 64;
    public int WallThresholdSize = 50;
    public int RoomThresholdSize = 50;
    public int PassageRadius = 2;
    public int SquareSize = 20;
    public int TileSize = 10;
    public string Seed = default!;
  }

  #region  CLASSES
  public struct Coord {
    public int TileX;
    public int TileY;

    public Coord(int x, int y) {
      TileX = x;
      TileY = y;
    }
  }

  internal struct Triangle {
    internal int VertexIndexA;
    internal int VertexIndexB;
    internal int VertexIndexC;
    internal int[] Vertices;

    internal Triangle(int a, int b, int c) {
      VertexIndexA = a;
      VertexIndexB = b;
      VertexIndexC = c;

      Vertices = new int[3];
      Vertices[0] = VertexIndexA;
      Vertices[1] = VertexIndexB;
      Vertices[2] = VertexIndexC;
    }

    internal int this[int i] {
      get {
        return Vertices[i];
      }
    }

    internal bool Contains(int vertexIndex) {
      return vertexIndex == VertexIndexA || vertexIndex == VertexIndexB || vertexIndex == VertexIndexC;
    }
  }

  internal class Node {
    internal Vector3 Position;
    internal int VertexIndex = -1;

    internal Node(Vector3 pos) {
      Position = pos;
    }
  }

  internal class ControlNode : Node {
    internal bool Active;
    internal Node Above;
    internal Node Right;

    internal ControlNode(Vector3 pos, bool active, float squareSize) : base(pos) {
      Active = active;
      Above = new(Position + Vector3.UnitZ * squareSize / 2f);
      Right = new(Position + Vector3.UnitX * squareSize / 2f);
    }
  }

  internal class Square {
    internal ControlNode TopLeft;
    internal ControlNode TopRight;
    internal ControlNode BottomRight;
    internal ControlNode BottomLeft;
    internal Node CenterTop;
    internal Node CenterRight;
    internal Node CenterBottom;
    internal Node CenterLeft;

    internal int Configuration;

    internal Square(
      ControlNode topLeft, ControlNode topRight,
      ControlNode bottomRight, ControlNode bottomLeft
    ) {
      TopLeft = topLeft;
      TopRight = topRight;
      BottomRight = bottomRight;
      BottomLeft = bottomLeft;

      CenterTop = TopLeft.Right;
      CenterRight = BottomRight.Above;
      CenterBottom = BottomLeft.Right;
      CenterLeft = BottomLeft.Above;

      if (TopLeft.Active) Configuration += 8;
      if (TopRight.Active) Configuration += 4;
      if (BottomRight.Active) Configuration += 2;
      if (BottomLeft.Active) Configuration += 1;
    }
  }

  internal class SquareGrid {
    internal Square[,] Squares;
    internal List<Vector3> Vertices;
    internal List<uint> Triangles;
    internal Dictionary<int, List<Triangle>> TriangleDictionary;
    internal List<List<int>> Outlines;
    internal HashSet<int> CheckedVertices;

    private readonly int[,] _map;
    private readonly float _squareSize;
    private readonly int _tileSize;

    internal SquareGrid(int[,] map, float squareSize, int tileSize) {
      int nodeCountX = map.GetLength(0);
      int nodeCountY = map.GetLength(1);
      float mapWidth = nodeCountX * squareSize;
      float mapHeight = nodeCountY * squareSize;

      _map = map;
      _squareSize = squareSize;
      _tileSize = tileSize;

      var controlNodes = new ControlNode[nodeCountX, nodeCountY];

      for (int x = 0; x < nodeCountX; x++) {
        for (int y = 0; y < nodeCountY; y++) {
          var pos = new Vector3(
            -mapWidth / 2 + x * squareSize + squareSize / 2,
            0,
            -mapHeight / 2 + y * squareSize + squareSize / 2
          );
          controlNodes[x, y] = new(pos, map[x, y] == 1, squareSize);
        }
      }

      Squares = new Square[nodeCountX - 1, nodeCountY - 1];
      for (int x = 0; x < nodeCountX - 1; x++) {
        for (int y = 0; y < nodeCountY - 1; y++) {
          Squares[x, y] = new Square(
            controlNodes[x, y + 1],
            controlNodes[x + 1, y + 1],
            controlNodes[x + 1, y],
            controlNodes[x, y]
          );
        }
      }

      Vertices = [];
      Triangles = [];
      TriangleDictionary = [];
      Outlines = [];
      CheckedVertices = [];
    }

    internal void GenerateMesh() {
      Outlines.Clear();
      CheckedVertices.Clear();
      TriangleDictionary.Clear();

      for (int x = 0; x < Squares.GetLength(0); x++) {
        for (int y = 0; y < Squares.GetLength(1); y++) {
          TriangulateSquare(Squares[x, y]);
        }
      }
    }

    internal void GenerateWallMesh(Application app, int wallHeight, out Mesh wallMesh) {
      CalculateMeshOutlines();

      var wallVertices = new List<Vector3>();
      var wallTriangles = new List<uint>();
      wallMesh = new Mesh(app.Allocator, app.Device, Matrix4x4.Identity);

      foreach (var outline in Outlines) {
        for (int i = 0; i < outline.Count - 1; i++) {
          var startIndex = wallVertices.Count;
          wallVertices.Add(Vertices[outline[i]]); // left vertex
          wallVertices.Add(Vertices[outline[i + 1]]); // right vertex
          wallVertices.Add(Vertices[outline[i]] - (Vector3.UnitY * wallHeight)); // bottom left vertex
          wallVertices.Add(Vertices[outline[i + 1]] - (Vector3.UnitY * wallHeight)); // bottom right vertex

          wallTriangles.Add((uint)startIndex + 0);
          wallTriangles.Add((uint)startIndex + 2);
          wallTriangles.Add((uint)startIndex + 3);

          wallTriangles.Add((uint)startIndex + 3);
          wallTriangles.Add((uint)startIndex + 1);
          wallTriangles.Add((uint)startIndex + 0);
        }
      }

      wallMesh.Vertices = wallVertices.Select(x => {
        return new Vertex() {
          Position = x,
          Color = new(1.0f, 1.0f, 1.0f),
          Normal = Vector3.UnitY,
        };
      }).ToArray();
      wallMesh.VertexCount = (ulong)wallVertices.Count;
      wallMesh.Indices = [.. wallTriangles];
      wallMesh.IndexCount = (ulong)wallTriangles.Count;

      for (int i = 0; i < wallVertices.Count; i++) {
        float percentX = Float.InverseLerp(
          -_map.GetLength(0) / 2 * _squareSize,
          _map.GetLength(0) / 2 * _squareSize,
          wallVertices[i].X
        ) * _tileSize;
        float percentY = Float.InverseLerp(
          -_map.GetLength(1) / 2 * _squareSize,
          _map.GetLength(1) / 2 * _squareSize,
          wallVertices[i].Y
        ) * _tileSize;
        wallMesh.Vertices[i].Uv = new(percentX, percentY);
      }
    }

    internal void TriangulateSquare(Square square) {
      switch (square.Configuration) {
        case 0:
          break;

        // 1 point cases
        case 1:
          MeshFromPoints(square.CenterLeft, square.CenterBottom, square.BottomLeft);
          break;
        case 2:
          MeshFromPoints(square.BottomRight, square.CenterBottom, square.CenterRight);
          break;
        case 4:
          MeshFromPoints(square.TopRight, square.CenterRight, square.CenterTop);
          break;
        case 8:
          MeshFromPoints(square.TopLeft, square.CenterTop, square.CenterLeft);
          break;

        // 2 point cases
        case 3:
          MeshFromPoints(square.CenterRight, square.BottomRight, square.BottomLeft, square.CenterLeft);
          break;
        case 6:
          MeshFromPoints(square.CenterTop, square.TopRight, square.BottomRight, square.CenterBottom);
          break;
        case 9:
          MeshFromPoints(square.TopLeft, square.CenterTop, square.CenterBottom, square.BottomLeft);
          break;
        case 12:
          MeshFromPoints(square.TopLeft, square.TopRight, square.CenterRight, square.CenterLeft);
          break;
        case 5:
          MeshFromPoints(square.CenterTop, square.TopRight, square.CenterRight, square.CenterBottom, square.BottomLeft, square.CenterLeft);
          break;
        case 10:
          MeshFromPoints(square.TopLeft, square.CenterTop, square.CenterRight, square.BottomRight, square.CenterBottom, square.CenterLeft);
          break;

        // 3 point cases
        case 7:
          MeshFromPoints(square.CenterTop, square.TopRight, square.BottomRight, square.BottomLeft, square.CenterLeft);
          break;
        case 11:
          MeshFromPoints(square.TopLeft, square.CenterTop, square.CenterRight, square.BottomRight, square.BottomLeft);
          break;
        case 13:
          MeshFromPoints(square.TopLeft, square.TopRight, square.CenterRight, square.CenterBottom, square.BottomLeft);
          break;
        case 14:
          MeshFromPoints(square.TopLeft, square.TopRight, square.BottomRight, square.CenterBottom, square.CenterLeft);
          break;

        // 4 point case
        case 15:
          MeshFromPoints(square.TopLeft, square.TopRight, square.BottomRight, square.BottomLeft);
          CheckedVertices.Add(square.TopLeft.VertexIndex);
          CheckedVertices.Add(square.TopRight.VertexIndex);
          CheckedVertices.Add(square.BottomRight.VertexIndex);
          CheckedVertices.Add(square.BottomLeft.VertexIndex);
          break;
      }
    }

    internal void MeshFromPoints(params Node[] points) {
      AssignVertices(points);

      if (points.Length >= 3) CreateTriangle(points[0], points[1], points[2]);
      if (points.Length >= 4) CreateTriangle(points[0], points[2], points[3]);
      if (points.Length >= 5) CreateTriangle(points[0], points[3], points[4]);
      if (points.Length >= 6) CreateTriangle(points[0], points[4], points[5]);
    }

    internal void AssignVertices(Node[] points) {
      for (int i = 0; i < points.Length; i++) {
        if (points[i].VertexIndex == -1) {
          points[i].VertexIndex = Vertices.Count;
          Vertices.Add(points[i].Position);
        }
      }
    }

    internal void CreateTriangle(Node a, Node b, Node c) {
      Triangles.AddRange([(uint)a.VertexIndex, (uint)b.VertexIndex, (uint)c.VertexIndex]);

      var triangle = new Triangle(a.VertexIndex, b.VertexIndex, c.VertexIndex);
      AddTriangleToDictionary(triangle.VertexIndexA, triangle);
      AddTriangleToDictionary(triangle.VertexIndexB, triangle);
      AddTriangleToDictionary(triangle.VertexIndexC, triangle);
    }

    internal void AddTriangleToDictionary(int vertexIndexKey, Triangle triangle) {
      if (TriangleDictionary.ContainsKey(vertexIndexKey)) {
        TriangleDictionary[vertexIndexKey].Add(triangle);
      } else {
        var triangleList = new List<Triangle> {
          triangle
        };
        TriangleDictionary.Add(vertexIndexKey, triangleList);
      }
    }

    internal void CalculateMeshOutlines() {
      for (int vertexIndex = 0; vertexIndex < Vertices.Count; vertexIndex++) {
        if (!CheckedVertices.Contains(vertexIndex)) {
          var newOutlineVertex = GetconnectedOutlineVertex(vertexIndex);
          if (newOutlineVertex != -1) {
            CheckedVertices.Add(vertexIndex);

            var newOutline = new List<int> {
              vertexIndex
            };

            Outlines.Add(newOutline);
            FollowOutline(newOutlineVertex, Outlines.Count - 1);
            Outlines[^1].Add(vertexIndex);
          }
        }
      }
    }

    internal void FollowOutline(int vertexIndex, int outlineIndex) {
      Outlines[outlineIndex].Add(vertexIndex);
      CheckedVertices.Add(vertexIndex);
      var nextVertexIndex = GetconnectedOutlineVertex(vertexIndex);

      if (nextVertexIndex != -1) {
        FollowOutline(nextVertexIndex, outlineIndex);
      }
    }

    internal bool IsOutlineEdge(int vertexA, int vertexB) {
      var trianglesContainingVertexA = TriangleDictionary[vertexA];
      int sharedTriangleCount = 0;

      for (int i = 0; i < trianglesContainingVertexA.Count; i++) {
        if (trianglesContainingVertexA[i].Contains(vertexB)) {
          sharedTriangleCount++;
          if (sharedTriangleCount > 1) {
            break;
          }
        }
      }

      return sharedTriangleCount == 1;
    }

    internal int GetconnectedOutlineVertex(int vertexIndex) {
      var trianglesContainingVertex = TriangleDictionary[vertexIndex];

      for (int i = 0; i < trianglesContainingVertex.Count; i++) {
        var triangle = trianglesContainingVertex[i];
        for (int j = 0; j < 3; j++) {
          var vertexB = triangle[j];

          if (vertexB != vertexIndex && !CheckedVertices.Contains(vertexB)) {
            if (IsOutlineEdge(vertexIndex, vertexB)) {
              return vertexB;
            }
          }
        }
      }

      return -1;
    }
  }

  public class Room : IComparable<Room> {
    public List<Coord> Tiles;
    internal List<Coord> EdgeTiles;
    internal List<Room> ConnectedRooms;
    internal int RoomSize;
    internal bool IsAccessableFromMainRoom;
    internal bool IsMainRoom;

    internal Room() {
      Tiles = [];
      EdgeTiles = [];
      ConnectedRooms = [];
      RoomSize = 0;
    }

    internal Room(List<Coord> roomTiles, int[,] map) {
      Tiles = roomTiles;
      RoomSize = Tiles.Count;
      ConnectedRooms = [];
      EdgeTiles = [];
      foreach (var tile in Tiles) {
        for (int x = tile.TileX - 1; x <= tile.TileX + 1; x++) {
          for (int y = tile.TileY - 1; y <= tile.TileY + 1; y++) {
            if (x == tile.TileX || y == tile.TileY) {
              if (map[x, y] == 1) {
                EdgeTiles.Add(tile);
              }
            }
          }
        }
      }
    }

    internal static void ConnectRoom(Room a, Room b) {
      if (a.IsAccessableFromMainRoom) {
        b.SetAccessableFromMainRoom();
      } else if (b.IsAccessableFromMainRoom) {
        a.SetAccessableFromMainRoom();
      }
      a.ConnectedRooms.Add(b);
      b.ConnectedRooms.Add(a);
    }

    internal bool IsConnected(Room other) {
      return ConnectedRooms.Contains(other);
    }

    internal void SetAccessableFromMainRoom() {
      if (!IsAccessableFromMainRoom) {
        IsAccessableFromMainRoom = true;
        foreach (var connectedRoom in ConnectedRooms) {
          connectedRoom.SetAccessableFromMainRoom();
        }
      }
    }

    public int CompareTo(Room? other) {
      if (other == null) {
        return 0;
      }
      return other.RoomSize.CompareTo(RoomSize);
    }
  }

  #endregion
  #region  CAVE GENERATOR
  public CaveGenerator(Action<GeneratorOptions> options) {
    var config = new GeneratorOptions();
    options(config);

    Width = config.MapWidth;
    Height = config.MapHeight;
    FillPercent = config.FillPercent;
    SmoothIterationCount = config.SmoothIterationCount;
    BorderSize = config.BorderSize;
    WallHeight = config.WallHeight;
    WallThresholdSize = config.WallThresholdSize;
    RoomThresholdSize = config.RoomThresholdSize;
    PassageRadius = config.PassageRadius;
    SquareSize = config.SquareSize;
    TileSize = config.TileSize;

    Seed = config.Seed;
    if (string.IsNullOrEmpty(Seed)) {
      Seed = Time.CurrentTime.ToString();
    }
    Map = new int[Width, Height];
  }

  public void GenerateMap(Application app, string topTexture, string wallTexture, ref Entity dstTarget) {
    GenerateMap(app.Device, out var cave, out var wall);

    var meshRenderer = new MeshRenderer(app.Device, app.Renderer);
    meshRenderer.AddLinearNode(new() { Mesh = cave });
    meshRenderer.AddLinearNode(new() { Mesh = wall });

    dstTarget.AddComponent(meshRenderer);
    dstTarget.GetComponent<MeshRenderer>().Init(AABBFilter.Terrain);
    dstTarget.GetComponent<MeshRenderer>().BindToTexture(app.TextureManager, topTexture, 0);
    dstTarget.GetComponent<MeshRenderer>().BindToTexture(app.TextureManager, wallTexture, 1);
    dstTarget.GetComponent<MeshRenderer>().FilterMeInShader = true;

    // dstTarget.AddRigidbody(PrimitiveType.Convex, true, false);
    // dstTarget.GetComponent<MeshRenderer>().BindMultipleModelPartsToTexture(app.TextureManager, textureName);
  }

  public void GenerateMap(IDevice device, out Mesh mesh, out Mesh wallMesh) {
    RandomFillMap();
    for (int i = 0; i < SmoothIterationCount; i++) {
      SmoothMap();
    }

    ProcessMap();

    var borderedMap = new int[Width + BorderSize * 2, Height + BorderSize * 2];

    for (int x = 0; x < borderedMap.GetLength(0); x++) {
      for (int y = 0; y < borderedMap.GetLength(1); y++) {
        if (x >= BorderSize && x < Width + BorderSize && y >= BorderSize && y < Height + BorderSize) {
          borderedMap[x, y] = Map[x - BorderSize, y - BorderSize];
        } else {
          borderedMap[x, y] = 1;
        }
      }
    }

    GenerateMesh(device, borderedMap, out mesh, out wallMesh);
  }

  public Vector3 MapPointToWorld(int mapX, int mapY, float scale = 1.0f) {
    float worldOffsetX = -Width * SquareSize / 2.0f;
    float worldOffsetZ = -Height * SquareSize / 2.0f;

    float worldX = (worldOffsetX + mapX * SquareSize + SquareSize / 2.0f) * scale;
    float worldZ = (worldOffsetZ + mapY * SquareSize + SquareSize / 2.0f) * scale;

    float worldY = 0;

    return new Vector3(worldX, worldY, worldZ);
  }

  public Vector3 GetRoomCenterInWorldSpace(Room room, float scale = 1.0f) {
    if (room.Tiles.Count == 0) {
      throw new InvalidOperationException("The room has no tiles.");
    }

    var targetTile = room.Tiles[room.Tiles.Count / 2];

    var pos = CoordToWorldPoint(targetTile);
    pos.X *= scale;
    pos.Z *= scale;
    return pos;
  }

  public Vector3 CoordToWorldPoint(Coord tile) {
    return new Vector3(-Width / 2 + .5f + tile.TileX, 0, -Height / 2 + .5f + tile.TileY);
  }

  private void SmoothMap() {
    for (int x = 0; x < Width; x++) {
      for (int y = 0; y < Height; y++) {
        var neighbourWallTiles = GetSurroundingWallCount(x, y);

        if (neighbourWallTiles > 4) {
          Map[x, y] = 1;
        } else if (neighbourWallTiles < 4) {
          Map[x, y] = 0;
        }
      }
    }
  }

  private int GetSurroundingWallCount(int gX, int gY) {
    int wallCount = 0;

    for (int neighbourX = gX - 1; neighbourX <= gX + 1; neighbourX++) {
      for (int neighbourY = gY - 1; neighbourY <= gY + 1; neighbourY++) {
        if (IsInMapRange(neighbourX, neighbourY)) {
          if (neighbourX != gX || neighbourY != gY) {
            wallCount += Map[neighbourX, neighbourY];
          }
        } else {
          wallCount++;
        }
      }
    }

    return wallCount;
  }

  private void RandomFillMap() {
    var prng = new System.Random(Seed.GetHashCode());

    for (int x = 0; x < Width; x++) {
      for (int y = 0; y < Height; y++) {
        if (x == 0 || x == Width - 1 || y == 0 || y == Height - 1) {
          Map[x, y] = 1;
        } else {
          Map[x, y] = (prng.Next(0, 100) < FillPercent) ? 1 : 0;
        }
      }
    }
  }

  private void ProcessMap() {
    var wallRegions = GetRegions(1);
    foreach (var wallRegion in wallRegions) {
      if (wallRegion.Count < WallThresholdSize) {
        foreach (var tile in wallRegion) {
          Map[tile.TileX, tile.TileY] = 0;
        }
      }
    }

    var roomRegions = GetRegions(0);
    var roomsRemaining = new List<Room>();
    foreach (var roomRegion in roomRegions) {
      if (roomRegion.Count < RoomThresholdSize) {
        foreach (var tile in roomRegion) {
          Map[tile.TileX, tile.TileY] = 1;
        }
      } else {
        roomsRemaining.Add(new Room(roomRegion, Map));
      }
    }
    roomsRemaining.Sort();
    roomsRemaining[0].IsMainRoom = true;
    roomsRemaining[0].IsAccessableFromMainRoom = true;
    MainRoom = roomsRemaining[0];
    ConnectClosestRooms(roomsRemaining);
  }

  private void ConnectClosestRooms(List<Room> rooms, bool forceAccessibilityFromMainRoom = false) {
    var roomListA = new List<Room>();
    var roomListB = new List<Room>();

    if (forceAccessibilityFromMainRoom) {
      foreach (var room in rooms) {
        if (room.IsAccessableFromMainRoom) {
          roomListB.Add(room);
        } else {
          roomListA.Add(room);
        }
      }
    } else {
      roomListA = rooms;
      roomListB = rooms;
    }

    int bestDistance = 0;
    var bestCoordA = new Coord();
    var bestCoordB = new Coord();
    var bestRoomA = new Room();
    var bestRoomB = new Room();
    var possibleConnectionFound = false;

    foreach (var roomA in roomListA) {
      if (!forceAccessibilityFromMainRoom) {
        possibleConnectionFound = false;
        if (roomA.ConnectedRooms.Count > 0) {
          continue;
        }
      }
      foreach (var roomB in roomListB) {
        if (roomA == roomB || roomA.IsConnected(roomB)) continue;
        for (int tileIndexA = 0; tileIndexA < roomA.EdgeTiles.Count; tileIndexA++) {
          for (int tileIndexB = 0; tileIndexB < roomB.EdgeTiles.Count; tileIndexB++) {
            var tileA = roomA.EdgeTiles[tileIndexA];
            var tileB = roomB.EdgeTiles[tileIndexB];
            int distanceBetweenRooms =
              (int)MathF.Pow(tileA.TileX - tileB.TileX, 2) +
              (int)MathF.Pow(tileA.TileY - tileB.TileY, 2);

            if (distanceBetweenRooms < bestDistance || !possibleConnectionFound) {
              bestDistance = distanceBetweenRooms;
              possibleConnectionFound = true;
              bestCoordA = tileA;
              bestCoordB = tileB;
              bestRoomA = roomA;
              bestRoomB = roomB;
            }
          }
        }
      }
      if (possibleConnectionFound && !forceAccessibilityFromMainRoom) {
        CreatePassage(bestRoomA, bestRoomB, bestCoordA, bestCoordB);
      }
    }

    if (possibleConnectionFound && forceAccessibilityFromMainRoom) {
      CreatePassage(bestRoomA, bestRoomB, bestCoordA, bestCoordB);
      ConnectClosestRooms(rooms, true);
    }

    if (!forceAccessibilityFromMainRoom) {
      ConnectClosestRooms(rooms, true);
    }
  }

  private void CreatePassage(Room roomA, Room roomB, Coord tileA, Coord tileB) {
    Room.ConnectRoom(roomA, roomB);

    var line = GetLine(tileA, tileB);
    foreach (var coord in line) {
      DrawCircle(coord, PassageRadius);
    }
  }

  private void DrawCircle(Coord c, int r) {
    for (int x = -r; x <= r; x++) {
      for (int y = -r; y <= r; y++) {
        if (x * x + y * y <= r * r) {
          var drawX = c.TileX + x;
          var drawY = c.TileY + y;

          if (IsInMapRange(drawX, drawY)) {
            Map[drawX, drawY] = 0;
          }
        }
      }
    }
  }

  private List<Coord> GetLine(Coord src, Coord dst) {
    var line = new List<Coord>();

    var x = src.TileX;
    var y = src.TileY;

    var dx = dst.TileX - src.TileX;
    var dy = dst.TileY - src.TileY;

    var step = (int)MathF.Sign(dx);
    var gradStep = (int)MathF.Sign(dy);

    var longest = (int)MathF.Abs(dx);
    var shortest = (int)MathF.Abs(dy);

    var inverted = false;

    if (longest < shortest) {
      inverted = true;
      longest = (int)MathF.Abs(dy);
      shortest = (int)MathF.Abs(dx);
      step = (int)MathF.Sign(dy);
      gradStep = (int)MathF.Sign(dx);
    }

    int gradAcc = longest / 2;
    for (int i = 0; i < longest; i++) {
      line.Add(new(x, y));
      if (inverted) {
        y += step;
      } else {
        x += step;
      }

      gradAcc += shortest;
      if (gradAcc >= longest) {
        if (inverted) {
          x += gradStep;
        } else {
          y += gradStep;
        }
        gradAcc -= longest;
      }
    }

    return line;
  }

  private List<List<Coord>> GetRegions(int tileType) {
    var regions = new List<List<Coord>>();
    var mapFlags = new int[Width, Height];

    for (int x = 0; x < Width; x++) {
      for (int y = 0; y < Height; y++) {
        if (mapFlags[x, y] == 0 && Map[x, y] == tileType) {
          var newRegion = GetRegionTiles(x, y);
          regions.Add(newRegion);
          foreach (var tile in newRegion) {
            mapFlags[tile.TileX, tile.TileY] = 1;
          }
        }
      }
    }

    return regions;
  }

  private List<Coord> GetRegionTiles(int startX, int startY) {
    var tiles = new List<Coord>();
    var mapFlags = new int[Width, Height];
    var tileType = Map[startX, startY];

    var queue = new Queue<Coord>();
    queue.Enqueue(new Coord(startX, startY));
    mapFlags[startX, startY] = 1;

    while (queue.Count > 0) {
      var tile = queue.Dequeue();
      tiles.Add(tile);

      for (int x = tile.TileX - 1; x <= tile.TileX + 1; x++) {
        for (int y = tile.TileY - 1; y <= tile.TileY + 1; y++) {
          if (IsInMapRange(x, y) && (y == tile.TileY || x == tile.TileX)) {
            if (mapFlags[x, y] == 0 && Map[x, y] == tileType) {
              mapFlags[x, y] = 1;
              queue.Enqueue(new Coord(x, y));
            }
          }
        }
      }
    }

    return tiles;
  }

  private bool IsInMapRange(int x, int y) {
    return x >= 0 && x < Width && y >= 0 && y < Height;
  }

  private void GenerateMesh(IDevice device, int[,] map, out Mesh mesh, out Mesh wallMesh) {
    var grid = new SquareGrid(map, SquareSize, TileSize);
    grid.GenerateMesh();

    var app = Application.Instance;
    mesh = new Mesh(app.Allocator, app.Device, Matrix4x4.Identity) {
      Vertices = grid.Vertices.Select(x => {
        return new Vertex() {
          Position = x,
          Color = new(1.0f, 1.0f, 1.0f),
          Normal = Vector3.UnitY,
        };
      }).ToArray(),
      VertexCount = (ulong)grid.Vertices.Count,
      Indices = [.. grid.Triangles],
      IndexCount = (ulong)grid.Triangles.Count
    };

    for (int i = 0; i < grid.Vertices.Count; i++) {
      float percentX = Float.InverseLerp(
        -map.GetLength(0) / 2 * SquareSize,
        map.GetLength(0) / 2 * SquareSize,
        grid.Vertices[i].X
      ) * TileSize;
      float percentY = Float.InverseLerp(
        -map.GetLength(1) / 2 * SquareSize,
        map.GetLength(1) / 2 * SquareSize,
        grid.Vertices[i].Z
      ) * TileSize;
      mesh.Vertices[i].Uv = new(percentX, percentY);
    }

    grid.GenerateWallMesh(app, WallHeight, out wallMesh);
  }
  #endregion
}