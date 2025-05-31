using System.Numerics;
using Dwarf.AbstractionLayer;
using Dwarf.EntityComponentSystem;
using Dwarf.Extensions.Logging;
using Dwarf.Globals;
using Dwarf.Math;
using Dwarf.Rendering;
using Dwarf.Rendering.Renderer3D;
using Dwarf.Vulkan;

using JoltPhysicsSharp;
using Vortice.Vulkan;
using static Dwarf.Physics.JoltConfig;

namespace Dwarf.Physics;

public class Rigidbody : Component, IDisposable {
  private readonly IDevice _device = null!;
  private readonly nint _allocator = IntPtr.Zero;
  private IPhysicsBody _bodyInterface = null!;
  private MotionType _motionType = MotionType.Dynamic;
  private readonly MotionQuality _motionQuality = MotionQuality.LinearCast;
  private readonly bool _physicsControlRotation = false;
  private readonly float _inputRadius = 0.0f;
  private readonly bool _useMesh = true;
  private float _sizeX = 0.0f;
  private float _sizeY = 0.0f;
  private float _sizeZ = 0.0f;
  private float _offsetX = 0.0f;
  private float _offsetY = 0.0f;
  private float _offsetZ = 0.0f;

  private Mesh? _collisionShape;

  internal Entity Owner { get; init; }

  public Rigidbody(Entity owner) {
    Owner = owner;
  }

  public Rigidbody(
    nint allocator,
    IDevice device,
    PrimitiveType primitiveType,
    MotionType motionType,
    Vector3 size,
    Vector3 offset,
    bool flip = false,
    bool physicsControlRotation = false,
    bool useMesh = true
  ) {
    _device = device;
    _allocator = allocator;
    _motionType = motionType;
    PrimitiveType = primitiveType;
    Flipped = flip;
    _sizeX = size.X;
    _sizeY = size.Y;
    _sizeZ = size.Z;
    _offsetX = offset.X;
    _offsetY = offset.Y;
    _offsetZ = offset.Z;
    _physicsControlRotation = physicsControlRotation;
    _inputRadius = default;
    _useMesh = useMesh;
    Owner = owner;
  }

  public Rigidbody(
    nint allocator,
    IDevice device,
    PrimitiveType colliderShape,
    float inputRadius,
    MotionType motionType,
    bool flip = false,
    bool physicsControlRotation = false,
    bool useMesh = true
  ) {
    PrimitiveType = colliderShape;
    _allocator = allocator;
    _device = device;
    _inputRadius = inputRadius;
    Flipped = flip;
    _motionType = motionType;
    _physicsControlRotation = physicsControlRotation;
    _useMesh = useMesh;
    Owner = owner;
  }

  public Rigidbody(
    nint allocator,
    IDevice device,
    PrimitiveType primitiveType,
    float sizeX,
    float sizeY,
    float sizeZ,
    MotionType motionType,
    bool flip,
    bool physicsControlRotation = false,
    bool useMesh = true
  ) {
    _device = device;
    _allocator = allocator;
    PrimitiveType = primitiveType;
    Flipped = flip;
    _motionType = motionType;
    _sizeX = sizeX;
    _sizeY = sizeY;
    _sizeZ = sizeZ;
    _physicsControlRotation = physicsControlRotation;
    _useMesh = useMesh;
    Owner = owner;
  }

  public Rigidbody(
    nint allocator,
    IDevice device,
    PrimitiveType primitiveType,
    float sizeX,
    float sizeY,
    float sizeZ,
    float offsetX,
    float offsetY,
    float offsetZ,
    MotionType motionType,
    bool flip,
    bool physicsControlRotation = false,
    bool useMesh = true
  ) {
    _device = device;
    _allocator = allocator;
    PrimitiveType = primitiveType;
    Flipped = flip;
    _motionType = motionType;
    _sizeX = sizeX;
    _sizeY = sizeY;
    _sizeZ = sizeZ;
    _offsetX = offsetX;
    _offsetY = offsetY;
    _offsetZ = offsetZ;
    _physicsControlRotation = physicsControlRotation;
    _useMesh = useMesh;
    Owner = owner;
  }

  public void InitBase(Mesh? mesh = null) {
    if (Owner.CanBeDisposed) throw new Exception("Entity is being disposed");
    if (PrimitiveType == PrimitiveType.None) throw new Exception("Collider must have certain type!");
    if (_device == null) throw new Exception("Device cannot be null!");

    // if (Owner?.GetDrawable<IRender3DElement>() == null) return;
    // var target = Owner!.GetDrawable<IRender3DElement>() as IRender3DElement;

    // var t = Owner!.GetComponent<Transform>();
    // if (_offsetX == 0.0f && _offsetY == 0.0f && _offsetZ == 0.0f) {
    //   _offsetX = t.Position.X + 6;
    //   _offsetY = t.Position.Y;
    //   _offsetZ = t.Position.Z - 10;
    // }

    // if (_sizeX == 0.0f && _sizeY == 0.0f && _sizeY == 0.0f) {
    //   _sizeX = t.Scale.X;
    //   _sizeY = t.Scale.Y;
    //   _sizeZ = t.Scale.Z;
    // }

    switch (PrimitiveType) {
      case PrimitiveType.Cylinder:
        _collisionShape = Primitives.CreateCylinderPrimitive(1, 1, 20);
        ScaleColliderMesh(ref _collisionShape);
        AdjustColliderMesh(ref _collisionShape);
        break;
      case PrimitiveType.Convex:
        if (mesh == null) {
          var target = (Owner?.GetDrawable3D()) ?? throw new ArgumentException(nameof(mesh));
          _collisionShape = Primitives.CreateConvex(Application.Instance, target!.MeshedNodes, Flipped);
        } else {
          _collisionShape = Primitives.CreateConvex(mesh, Flipped);
        }
        AdjustColliderMesh(ref _collisionShape);
        // ScaleColliderMesh(ref mesh);

        break;
      case PrimitiveType.Box:
        _collisionShape = Primitives.CreateBoxPrimitive(1);
        ScaleColliderMesh(ref _collisionShape);
        AdjustColliderMesh(ref _collisionShape);
        break;
      default:
        _collisionShape = Primitives.CreateBoxPrimitive(1);
        break;
    }

    if (_useMesh) {
      Owner!.AddComponent(new ColliderMesh(_allocator, _device, _collisionShape));
    }

  }

  public unsafe void Init(in IPhysicsBody bodyInterface) {
    if (Owner.CanBeDisposed) throw new Exception("Entity is being disposed");
    if (PrimitiveType == PrimitiveType.None) throw new Exception("Collider must have certain type!");
    if (_collisionShape == null) throw new ArgumentNullException(nameof(_collisionShape));
    if (_device == null) throw new Exception("Device cannot be null!");

    _bodyInterface = bodyInterface;

    var pos = Owner!.GetTransform()!.Position;
    object shapeSettings;

    switch (PrimitiveType) {
      case PrimitiveType.Cylinder:
        shapeSettings = _bodyInterface.ColldierMeshToPhysicsShape(Owner, _collisionShape);
        break;
      case PrimitiveType.Convex:
        shapeSettings = _bodyInterface.ColldierMeshToPhysicsShape(Owner, _collisionShape);
        break;
      case PrimitiveType.Box:
        shapeSettings = _bodyInterface.ColldierMeshToPhysicsShape(Owner, _collisionShape);
        break;
      default:
        // shapeSettings = new BoxShapeSettings(new(1 / 2, 1 / 2, 1 / 2));
        throw new NotImplementedException();
    }

    _bodyInterface.CreateAndAddBody(_motionType, shapeSettings, pos);

    _bodyInterface.GravityFactor = 0.1f;
    _bodyInterface.MotionQuality = _motionQuality;
    _bodyInterface.MotionType = _motionType;
  }

  private void AdjustColliderMesh(ref Mesh colliderMesh) {
    for (int i = 0; i < colliderMesh.Vertices.Length; i++) {
      colliderMesh.Vertices[i].Position.X += _offsetX;
      colliderMesh.Vertices[i].Position.Y += _offsetY;
      colliderMesh.Vertices[i].Position.Z += _offsetZ;
    }
  }

  private void ScaleColliderMesh(ref Mesh colliderMesh) {
    for (int i = 0; i < colliderMesh.Vertices.Length; i++) {
      colliderMesh.Vertices[i].Position.X *= _sizeX;
      colliderMesh.Vertices[i].Position.Y *= _sizeY;
      colliderMesh.Vertices[i].Position.Z *= _sizeZ;
    }
  }

  public void Update() {
    if (Owner.CanBeDisposed) return;
    if (_bodyInterface == null) return;

    var pos = _bodyInterface.Position;
    var transform = Owner!.GetTransform()!;

    transform.Position = pos;

    if (!_physicsControlRotation) {
      var quat = Quaternion.CreateFromRotationMatrix(transform.AngleY());
      _bodyInterface.Rotation = quat;
    } else {
      transform.Rotation = Quat.ToEuler(_bodyInterface.Rotation);
    }

    if (_motionType == MotionType.Dynamic) {
      _bodyInterface.LinearVelocity /= 2;

      if (_bodyInterface.LinearVelocity.Y < 0) _bodyInterface.SetLinearVelocity(0, 1);
    } else {
      _bodyInterface.LinearVelocity /= 2;
      _bodyInterface.SetLinearVelocity(0, 1);
    }

    // freeze rigidbody to X an Z axis
    // _bodyInterface.SetRotation(_bodyId, new System.Numerics.Quaternion(0.0f, rot.Y, 0.0f, 1.0f), Activation.Activate);
  }

  public void AddForce(Vector3 vec3) {
    if (Owner.CanBeDisposed) return;
    _bodyInterface.AddForce(vec3);
  }

  public void AddVelocity(Vector3 vec3) {
    if (Owner.CanBeDisposed) return;
    _bodyInterface.AddLinearVelocity(vec3);
  }

  public void AddImpulse(Vector3 vec3) {
    if (Owner.CanBeDisposed) return;
    _bodyInterface.AddImpulse(vec3);
  }

  public void Translate(Vector3 vec3) {
    if (Owner.CanBeDisposed) return;
    _bodyInterface.AddLinearVelocity(vec3);
  }

  public void MoveKinematic(Vector3 pos, float speed) {
    if (Owner.CanBeDisposed) return;
    _bodyInterface.MoveKinematic(speed, pos, default);
  }

  public void SetHorizontalVelocity(Vector3 desiredXZ) {
    if (Owner.CanBeDisposed) return;
    var v = _bodyInterface.LinearVelocity;
    v.X = desiredXZ.X;
    v.Z = desiredXZ.Z;
    _bodyInterface.LinearVelocity = v;
  }

  public void Rotate(Vector3 vec3) {
    if (Owner.CanBeDisposed) return;
    var rot = _bodyInterface.Rotation;
    rot.X += vec3.X;
    rot.Y += vec3.Y;
    rot.Z += vec3.Z;
    _bodyInterface.Rotation = rot;
  }

  public void SetRotation(Vector3 vec3) {
    if (Owner.CanBeDisposed) return;
    var rot = _bodyInterface.Rotation;
    _bodyInterface.Rotation = new(vec3, rot.Z);
  }

  public void SetPosition(Vector3 vec3) {
    if (Owner.CanBeDisposed) return;
    _bodyInterface.Position = vec3;
  }

  public Vector3 Velocity {
    get {
      if (Owner.CanBeDisposed) return Vector3.Zero;
      return _bodyInterface.LinearVelocity;
    }
  }

  public bool Moving {
    get {
      var vel = _bodyInterface.LinearVelocity;
      var mag = vel.X * vel.X + vel.Z * vel.Z;
      const float tol = 0.05f;
      return mag > tol * tol;
    }
  }

  public void InvokeCollision(CollisionState collisionState, Entity otherColl) {
    if (Owner.CanBeDisposed) return;
    var scripts = Owner!.GetScripts();
    for (short i = 0; i < scripts.Length; i++) {
      switch (collisionState) {
        case CollisionState.Enter:
          scripts[i].CollisionEnter(otherColl);
          break;
        case CollisionState.Stay:
          scripts[i].CollisionStay(otherColl);
          break;
        case CollisionState.Exit:
          scripts[i].CollisionExit(otherColl);
          break;
        default:
          break;
      }
    }
  }


  public bool Kinematic {
    get {
      return _motionType == MotionType.Static;
    }
    set {
      _motionType = value ? MotionType.Static : MotionType.Dynamic;
    }
  }

  public MotionType MotionType => _motionType;

  public Vector3 Offset => new(_offsetX, _offsetY, _offsetZ);
  public Vector3 Size => new(_sizeX, _sizeY, _sizeZ);
  public Quaternion Rotation => _bodyInterface.Rotation;
  public bool Flipped { get; } = false;
  public IPhysicsBody BodyInterface => _bodyInterface;

  public PrimitiveType PrimitiveType { get; } = PrimitiveType.None;

  public void Dispose() {
    GC.SuppressFinalize(this);
  }
}
