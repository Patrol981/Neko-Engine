using System.Numerics;
using Dwarf.Extensions.Logging;
using Dwarf.Globals;
using Dwarf.Math;
using Dwarf.Physics;
using Dwarf.Physics.Interfaces;
using Dwarf.Rendering;
using Dwarf.Vulkan;

namespace Dwarf.EntityComponentSystem;

public class Rigidbody2D : IDisposable, ICloneable {
  private readonly Application _app;
  private readonly nint _allocator = IntPtr.Zero;
  public IPhysicsBody2D PhysicsBody2D { get; private set; } = null!;
  private Mesh? _collisionShape;
  public Vector2 Min { get; private set; } = Vector2.Zero;
  public Vector2 Max { get; private set; } = Vector2.Zero;
  public MotionType MotionType { get; init; } = MotionType.Dynamic;
  public PrimitiveType PrimitiveType { get; init; } = PrimitiveType.None;
  public bool IsTrigger { get; private set; } = false;

  public Vector2 Velocity => Vector2.Zero;
  public bool Kinematic => false;
  public bool Grounded { get; private set; }

  public Entity? Owner { get; internal set; }
  private TransformComponent? _transform;

  public Rigidbody2D() {
    _app = Application.Instance;
    _allocator = _app.Allocator;
    PrimitiveType = PrimitiveType.Box;
  }

  public Rigidbody2D(
    Application app,
    PrimitiveType primitiveType,
    MotionType motionType,
    bool isTrigger
  ) {
    _app = app;
    _allocator = _app.Allocator;
    MotionType = motionType;
    PrimitiveType = primitiveType;
    IsTrigger = isTrigger;
  }

  public Rigidbody2D(
    Application app,
    PrimitiveType primitiveType,
    MotionType motionType,
    Vector2 min,
    Vector2 max,
    bool isTrigger
  ) {
    _app = app;
    _allocator = _app.Allocator;
    PrimitiveType = primitiveType;
    MotionType = motionType;
    Min = min;
    Max = max;
    IsTrigger = isTrigger;
  }

  private Rigidbody2D(
    Application app,
    nint allocator
  ) {
    _app = app;
    _allocator = allocator;
  }

  public void Init(in IPhysicsBody2D physicsBody2D) {
    if (Owner == null) throw new NullReferenceException("Owner cannot be null");
    if (Owner.CanBeDisposed) throw new Exception("Entity is being disposed");
    if (PrimitiveType == PrimitiveType.None) throw new Exception("Collider must have certain type!");
    if (_collisionShape == null) throw new ArgumentNullException(nameof(_collisionShape));
    if (_app.Device == null) throw new Exception("Device cannot be null!");

    PhysicsBody2D = physicsBody2D;

    var pos = Owner.GetTransform()!.Position;
    var shapeSettings = PhysicsBody2D.ColldierMeshToPhysicsShape(Owner, _collisionShape);
    PhysicsBody2D.CreateAndAddBody(MotionType, shapeSettings, pos.ToVector2(), IsTrigger);
    PhysicsBody2D.GravityFactor = 0.1f;
  }

  public void InitBase(bool scaleMinMax = true) {
    if (Owner == null) throw new NullReferenceException("Owner cannot be null");
    Application.Mutex.WaitOne();
    if (scaleMinMax) {
      var scale = Owner.GetTransform()!.Scale;
      Min = new Vector2(Min.X * scale.X, Min.Y * scale.Y);
      Max = new Vector2(Max.X * scale.X, Max.Y * scale.Y);
    }

    _collisionShape = PrimitiveType switch {
      PrimitiveType.Convex => GetFromOwner(),
      PrimitiveType.Box => Primitives2D.CreateQuad2D(Min, Max),
      _ => throw new NotImplementedException(),
    };

    // Owner.AddComponent(new ColliderMesh(_app.Allocator, _app.Device, _collisionShape!));
    Owner.AddComponent(new ColliderMesh(Owner, _app.Allocator, (VulkanDevice)_app.Device, _collisionShape!));
    Application.Mutex.ReleaseMutex();
  }

  private Mesh GetFromOwner() {
    var mesh = Owner!.GetDrawable2D()!.CollisionMesh.Clone() as Mesh;
    var scale = Owner!.GetTransform()!.Scale;
    Logger.Info($"Scale to apply {scale}");
    for (int i = 0; i < mesh!.Vertices.Length; i++) {
      mesh.Vertices[i].Position.X *= scale.X;
      mesh.Vertices[i].Position.Y *= scale.Y;
      mesh.Vertices[i].Position.Z *= scale.Z;
    }

    return mesh;
  }

  public void Update() {
    if (Owner == null || Owner.CanBeDisposed) return;

    var pos = PhysicsBody2D?.Position;
    var transform = Owner.GetTransform();

    if (transform == null) return;

    transform.Position.X = pos.HasValue ? pos.Value.X : 0;
    transform.Position.Y = pos.HasValue ? pos.Value.Y : 0;

    Grounded = PhysicsBody2D?.Grounded ?? false;
  }

  public void AddForce(Vector2 vec2) {
    if (Owner == null || Owner.CanBeDisposed) return;
    PhysicsBody2D.AddForce(vec2);
  }

  public void AddVelocity(Vector2 vec2) {
    if (Owner == null || Owner.CanBeDisposed) return;
    PhysicsBody2D.AddLinearVelocity(vec2);
  }

  public void AddImpule(Vector2 vec2) {
    if (Owner == null || Owner.CanBeDisposed) return;
    PhysicsBody2D.AddImpulse(vec2);
  }

  public void Translate(Vector2 vec2) {
    if (Owner == null || Owner.CanBeDisposed) return;
    PhysicsBody2D.AddLinearVelocity(vec2);
  }

  public void SetPosition(Vector2 vec2) {
    if (Owner == null || Owner.CanBeDisposed) return;
    PhysicsBody2D.Position = vec2;
  }

  public void InvokeCollision(CollisionState collisionState, Entity? otherColl, bool otherTrigger) {
    if (Owner == null || Owner.CanBeDisposed) return;
    var scripts = Owner!.GetScripts();
    for (short i = 0; i < scripts.Length; i++) {
      switch (collisionState) {
        case CollisionState.Enter:
          scripts[i].CollisionEnter(otherColl, otherTrigger);
          break;
        case CollisionState.Stay:
          scripts[i].CollisionStay(otherColl, otherTrigger);
          break;
        case CollisionState.Exit:
          scripts[i].CollisionExit(otherColl, otherTrigger);
          break;
        default:
          break;
      }
    }
  }

  public void Dispose() {
    PhysicsBody2D?.Dispose();
    GC.SuppressFinalize(this);
  }

  public object Clone() {
    var rb = new Rigidbody2D(_app, _allocator) {
      MotionType = MotionType,
      Max = Max,
      Min = Min,
      PrimitiveType = PrimitiveType,
      IsTrigger = IsTrigger
    };
    return rb;
  }
}