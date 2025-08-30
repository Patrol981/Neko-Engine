using System.Numerics;
using Dwarf.Physics;
using Dwarf.Rendering;

namespace Dwarf.EntityComponentSystem;

public static class RigidbodyExtensions {
  public static void AddRigidbody2D(
    this Entity entity,
    PrimitiveType primitiveType,
    MotionType motionType,
    Vector2 min,
    Vector2 max,
    bool isTrigger = false
  ) {
    if (entity.CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");
    var guid = Guid.NewGuid();
    try {
      Application.Mutex.WaitOne();
      entity.Components.Add(typeof(Rigidbody2D), guid);
      var rb2D = new Rigidbody2D(Application.Instance, primitiveType, motionType, min, max, isTrigger) {
        Owner = entity
      };
      rb2D.InitBase();
      if (!Application.Instance.Rigidbodies2D.TryAdd(guid, rb2D)) {
        throw new Exception("Cannot add transform to list");
      }
      Application.Mutex.ReleaseMutex();
    } catch {
      Application.Mutex.ReleaseMutex();
      throw;
    }
  }

  public static void AddRigidbody2D(
  this Entity entity,
  PrimitiveType primitiveType,
  MotionType motionType,
  bool isTrigger = false
) {
    if (entity.CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");
    var guid = Guid.NewGuid();
    try {
      Application.Mutex.WaitOne();
      entity.Components.Add(typeof(Rigidbody2D), guid);
      var rb2D = new Rigidbody2D(Application.Instance, primitiveType, motionType, isTrigger) {
        Owner = entity
      };
      rb2D.InitBase();
      if (!Application.Instance.Rigidbodies2D.TryAdd(guid, rb2D)) {
        throw new Exception("Cannot add transform to list");
      }
      Application.Mutex.ReleaseMutex();
    } catch {
      Application.Mutex.ReleaseMutex();
      throw;
    }
  }

  public static Rigidbody2D? GetRigidbody2D(this Entity entity) {
    if (entity.CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");
    if (entity.Components.TryGetValue(typeof(Rigidbody2D), out var guid)) {
      var result = Application.Instance.Rigidbodies2D[guid];
      return result;
    } else {
      return null;
    }
  }

  public static void AddRigidbody(
  Application app,
  ref Entity entity,
  in Mesh mesh,
  PrimitiveType primitiveType,
  Vector3 size,
  Vector3 offset,
  MotionType motionType = MotionType.Dynamic,
  bool flip = false,
  bool useMesh = true
) {
    if (entity == null) return;

    var rb = new Rigidbody(
      entity,
      app.Allocator,
      app.Device,
      primitiveType,
      motionType,
      size: size,
      offset: offset,
      flip,
      useMesh: useMesh
    );

    var guid = Guid.NewGuid();
    try {
      entity.Components.TryAdd(typeof(Rigidbody), guid);
      Application.Instance.Rigidbodies.TryAdd(guid, rb);
    } catch {
      throw;
    }

    entity.GetRigidbody()?.InitBase(mesh);
  }

  public static void AddRigidbody(
    Application app,
    ref Entity entity,
    PrimitiveType primitiveType,
    float sizeX = 1,
    float sizeY = 1,
    float sizeZ = 1,
    MotionType motionType = MotionType.Dynamic,
    bool flip = false,
    bool useMesh = true
  ) {
    if (entity == null) return;

    var rb = new Rigidbody(entity, app.Allocator, app.Device, primitiveType, sizeX, sizeY, sizeZ, motionType, flip, useMesh: useMesh);

    var guid = Guid.NewGuid();
    try {
      entity.Components.TryAdd(typeof(Rigidbody), guid);
      Application.Instance.Rigidbodies.TryAdd(guid, rb);
    } catch {
      throw;
    }

    entity.GetRigidbody()?.InitBase();
  }

  public static void AddRigidbody(
    Application app,
    ref Entity entity,
    PrimitiveType primitiveType,
    float sizeX = 1,
    float sizeY = 1,
    float sizeZ = 1,
    float offsetX = 0,
    float offsetY = 0,
    float offsetZ = 0,
    MotionType motionType = MotionType.Dynamic,
    bool flip = false,
    bool useMesh = true
  ) {
    if (entity == null) return;

    var rb = new Rigidbody(entity, app.Allocator, app.Device, primitiveType, sizeX, sizeY, sizeZ, offsetX, offsetY, offsetZ, motionType, flip, useMesh: useMesh);

    var guid = Guid.NewGuid();
    try {
      entity.Components.TryAdd(typeof(Rigidbody), guid);
      Application.Instance.Rigidbodies.TryAdd(guid, rb);
    } catch {
      throw;
    }

    entity.GetRigidbody()?.InitBase();
  }

  public static void AddRigidbody(this Entity entity, PrimitiveType primitiveType = PrimitiveType.Convex, MotionType motionType = MotionType.Dynamic, float radius = 1) {
    var app = Application.Instance;

    AddRigidbody(app, ref entity, primitiveType, radius, motionType);
  }

  public static void AddRigidbody(
    this Entity entity,
    PrimitiveType primitiveType = PrimitiveType.Convex,
    float sizeX = 1,
    float sizeY = 1,
    float sizeZ = 1,
    float offsetX = 0,
    float offsetY = 0,
    float offsetZ = 0,
    MotionType motionType = MotionType.Dynamic,
    bool flip = false
  ) {
    var app = Application.Instance;
    AddRigidbody(app, ref entity, primitiveType, sizeX, sizeY, sizeZ, offsetX, offsetY, offsetZ, motionType, flip);
  }

  public static void AddRigidbody(
    this Entity entity,
    PrimitiveType primitiveType = PrimitiveType.Convex,
    Vector3 size = default,
    Vector3 offset = default,
    MotionType motionType = MotionType.Dynamic,
    bool flip = false,
    bool useMesh = true
  ) {
    var app = Application.Instance;
    AddRigidbody(app, ref entity, primitiveType, size.X, size.Y, size.Z, offset.X, offset.Y, offset.Z, motionType: motionType, flip: flip, useMesh: useMesh);
  }

  public static void AddRigidbody(
    this Entity entity,
    PrimitiveType primitiveType = PrimitiveType.Convex,
    MotionType motionType = MotionType.Dynamic,
    bool flip = false,
    bool useMesh = true
  ) {
    var app = Application.Instance;
    AddRigidbody(app, ref entity, primitiveType, default, motionType: motionType, flip: flip, useMesh: useMesh);
  }

  public static void AddRigidbody(
    Application app,
    ref Entity entity,
    PrimitiveType primitiveType,
    float radius,
    MotionType motionType = MotionType.Dynamic,
    bool flip = false,
    bool useMesh = true
  ) {
    if (entity == null) return;

    var rb = new Rigidbody(entity, app.Allocator, app.Device, primitiveType, radius, motionType, flip, useMesh: useMesh);

    var guid = Guid.NewGuid();
    try {
      entity.Components.TryAdd(typeof(Rigidbody), guid);
      Application.Instance.Rigidbodies.TryAdd(guid, rb);
    } catch {
      throw;
    }

    entity.GetRigidbody()?.InitBase();
  }

  public static void AddRigidbody(
    Application app,
    ref Entity entity,
    in Mesh mesh,
    PrimitiveType primitiveType,
    float radius,
    MotionType motionType = MotionType.Dynamic,
    bool flip = false,
    bool useMesh = true
  ) {
    if (entity == null) return;

    var rb = new Rigidbody(entity, app.Allocator, app.Device, primitiveType, radius, motionType, flip, useMesh: useMesh);

    var guid = Guid.NewGuid();
    try {
      entity.Components.TryAdd(typeof(Rigidbody), guid);
      Application.Instance.Rigidbodies.TryAdd(guid, rb);
    } catch {
      throw;
    }

    entity.GetRigidbody()?.InitBase();
  }

  public static void AddRigidbody(
    this Entity entity,
    in Mesh mesh,
    PrimitiveType primitiveType = PrimitiveType.Convex,
    MotionType motionType = MotionType.Dynamic,
    bool flip = false
  ) {
    var app = Application.Instance;
    AddRigidbody(app, ref entity, mesh, primitiveType, default, motionType, flip);
  }

  public static void AddRigidbody(
    this Entity entity,
    in Mesh mesh,
    Vector3 size,
    Vector3 offset,
    PrimitiveType primitiveType = PrimitiveType.Convex,
    MotionType motionType = MotionType.Dynamic,
    bool flip = false
  ) {
    var app = Application.Instance;
    AddRigidbody(
      app,
      ref entity,
      mesh: in mesh,
      size: size,
      offset: offset,
      primitiveType: primitiveType,
      motionType: motionType,
      flip: flip
    );
  }

  public static Rigidbody? GetRigidbody(this Entity entity) {
    if (entity.CanBeDisposed) throw new ArgumentException("Cannot access disposed entity!");
    if (entity.Components.TryGetValue(typeof(Rigidbody), out var guid)) {
      var result = Application.Instance.Rigidbodies[guid];
      return result;
    } else {
      return null;
    }
  }
}