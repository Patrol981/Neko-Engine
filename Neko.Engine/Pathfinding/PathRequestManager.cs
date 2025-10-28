using System.Numerics;
using Neko.EntityComponentSystem;
using Neko.Extensions.Logging;

namespace Neko.Pathfinding.AStar;

public class PathRequestManager : NekoScript {
  private readonly Queue<PathRequest> _pathRequestQueue = new Queue<PathRequest>();
  private PathRequest _currentPathRequest = null!;
  private bool _isProcessingPath = false;

  private Pathfinder _pathfinder = null!;

  private Grid _grid = null!;
  private List<Vector3> _tmpPath = [];

  public override void Awake() {
    Instance = this;
    // _pathfinder = Owner!.GetComponent<Pathfinder>()!;
    // _grid = Owner!.GetComponent<Grid>();
  }

  public void PaintGuizmos() {
    return;

    // foreach (var node in _grid.GridData) {
    //   _grid.GridGuizmos[node.GridPosition.X, node.GridPosition.Y].Color = node.Walkable ? new(0.2f, 0.7f, 0.2f) : new(1.0f, 0.0f, 0.0f);
    //   if (_tmpPath != null && _tmpPath.Count > 0) {
    //     if (_tmpPath.Contains(node.WorldPosition)) {
    //       _grid.GridGuizmos[node.GridPosition.X, node.GridPosition.Y].Color = new(1.0f, 1.0f, 0.0f);
    //     }
    //   }
    // }
  }

  public override void Update() {
    // Logger.Info(_isProcessingPath);
    // Logger.Info(_pathRequestQueue.Count);
    // Logger.Info(_currentPathRequest == null);
  }

  public static void RequestPath(Vector3 pathStart, Vector3 pathEnd, Action<Vector3[], bool> callback) {
    Task.Run(() => {
      Instance._grid.CreateGrid();
      var newRequest = new PathRequest(pathStart, pathEnd, callback);
      Instance._pathRequestQueue.Enqueue(newRequest);
      Instance.TryProcessNext();
    });
  }

  public void FinishedProcessingPath(Vector3[] path, bool success) {
    _tmpPath = [.. path];
    _currentPathRequest.Callback(path, success);
    _isProcessingPath = false;
    _currentPathRequest = null!;
    // TryProcessNext();
  }

  private void TryProcessNext() {
    if (_currentPathRequest != null) return;
    if (!_isProcessingPath && _pathRequestQueue.Count > 0) {
      _currentPathRequest = _pathRequestQueue.Dequeue();
      _isProcessingPath = true;
      _pathfinder.StartFindPath(_currentPathRequest.PathStart, _currentPathRequest.PathEnd);
    }
  }

  public static PathRequestManager Instance { get; private set; } = null!;

  class PathRequest(Vector3 start, Vector3 end, Action<Vector3[], bool> callback) {
    public Vector3 PathStart = start;
    public Vector3 PathEnd = end;
    public Action<Vector3[], bool> Callback = callback;
  }
}