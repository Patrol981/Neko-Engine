using System.Collections;
using System.Numerics;
using Dwarf.Coroutines;
using Dwarf.EntityComponentSystem;
using Dwarf.Extensions.Logging;
using Dwarf.Globals;
using Dwarf.Pathfinding.AStar;
using Dwarf.Rendering.Renderer3D.Animations;

namespace Dwarf.Pathfinding;

public class Unit : DwarfScript {
  private float _speed = 0.5f;
  private Vector3[] _path = [];
  private int _targetIndex;
  private TransformComponent _transform = null!;
  private AnimationController? _animationController;

  public override void Awake() {
    var hasTransform = Owner!.HasComponent<TransformComponent>();
    if (!hasTransform) {
      Owner!.AddComponent(new TransformComponent());
    }
    _transform = Owner!.GetTransform()!;
    _animationController = Owner!.GetAnimationController();
  }

  public override void Start() {
    // PathRequestManager.RequestPath(Owner!.GetComponent<Transform>().Position, Target.Position, OnPathFound);
  }

  public override void Update() {
    if (IsMoving) {
      _animationController?.SetCurrentAnimation("Walking_A");
    } else {
      _animationController?.SetCurrentAnimation("Idle");
    }
  }

  public async void OnPathFound(Vector3[] newPath, bool pathSuccess) {
    if (pathSuccess && !IsMoving) {
      _path = newPath;
      _targetIndex = 0;
      IsMoving = true;
      await CoroutineRunner.Instance.StopCoroutine(FollowPath());
      CoroutineRunner.Instance.StartCoroutine(FollowPath());
    }
  }

  private IEnumerator FollowPath() {
    if (_path == null || _path.Length == 0) {
      IsMoving = false;
      yield break;
    }

    int currentWaypointIndex = 0;
    Vector3 currentWaypoint = _path[currentWaypointIndex];

    while (true) {
      var dir = Vector3.Normalize(currentWaypoint - _transform.Position);
      _transform.Position += dir * _speed * Time.DeltaTime;
      _transform.LookAtFixed(currentWaypoint);

      if (Vector3.Distance(_transform.Position, currentWaypoint) < 0.1f) {
        currentWaypointIndex++;
        if (currentWaypointIndex < _path.Length) {
          currentWaypoint = _path[currentWaypointIndex];
          yield return null;
        } else {
          _path = null!;
          IsMoving = false;
          yield break;
        }
      } else {
        yield return null;
      }
    }
  }

  private IEnumerator FollowPath_() {
    if (_path == null || _path.Length == 0) {
      IsMoving = false;
      yield break;
    }

    int currentWaypointIndex = 0;
    Vector3 currentWaypoint = _path[currentWaypointIndex];

    while (true) {
      _transform.LookAtFixed(currentWaypoint);
      if (Vector3.Distance(_transform.Position, currentWaypoint) < 0.1f) {
        currentWaypointIndex++;
        if (currentWaypointIndex >= _path.Length) {
          _path = null!;
          IsMoving = false;
          yield break;
        }
        currentWaypoint = _path[currentWaypointIndex];
      }

      _transform.Position = _transform.MoveTowards(_transform.Position, currentWaypoint, Time.StopwatchDelta);
      // _transform.Position = Vector3.Lerp(_transform.Position, currentWaypoint, _speed * Time.DeltaTime);
      yield return null;
    }
  }

  private IEnumerator FollowPath__() {
    if (_path == null || _path.Length == 0) {
      IsMoving = false;
      yield break;
    }

    int currentWaypointIndex = 0;
    Vector3 currentWaypoint = _path[currentWaypointIndex];

    while (true) {
      _transform.LookAtFixed(currentWaypoint);
      if (Vector3.Distance(_transform.Position, currentWaypoint) < 0.001f) { // Use a tolerance value for position comparison
        currentWaypointIndex++;
        if (currentWaypointIndex >= _path.Length) {
          _path = null!;
          IsMoving = false;
          yield break;
        }
        currentWaypoint = _path[currentWaypointIndex];
      }
      _transform.Position = _transform.MoveTowards(_transform.Position, currentWaypoint, _speed * Time.DeltaTime);
      yield return null;
    }
  }

  private IEnumerator FollowPath_Old() {
    if (_path == null) { IsMoving = false; yield break; }
    if (_path!.Length <= 0) { IsMoving = false; yield break; }

    var currentWaypoint = _path[0];
    _transform.LookAtFixed(currentWaypoint);

    if (currentWaypoint == _transform.Position) {
      IsMoving = false;
      yield break;
    }

    while (true) {
      if (_transform.Position == currentWaypoint) {
        _targetIndex += 1;
        if (_targetIndex >= _path.Length) {
          _path = null!;
          IsMoving = false;
          yield break;
        }
        currentWaypoint = _path[_targetIndex];
        _transform.LookAtFixed(currentWaypoint);
      }
      _transform.Position = _transform.MoveTowards(_transform.Position, currentWaypoint, _speed * Time.DeltaTime);
      yield return null;
    }
  }

  public bool IsMoving { get; private set; } = false;
}

