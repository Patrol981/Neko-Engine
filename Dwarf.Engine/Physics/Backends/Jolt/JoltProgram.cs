using System.Diagnostics;
using System.Numerics;
using Dwarf.EntityComponentSystem;
using Dwarf.Extensions.Logging;
using JoltPhysicsSharp;

using static Dwarf.Physics.JoltConfig;

namespace Dwarf.Physics.Backends.Jolt;

public class JoltProgram : IPhysicsProgram {
  // We simulate the physics world in discrete time steps. 60 Hz is a good rate to update the physics system.
  public float DeltaTime = 1.0f / 60.0f;
  public int CollisionSteps = 2;
  public JobSystem? JobSystem { get; private set; }

  public PhysicsSystemSettings JoltSettings;
  public readonly JoltPhysicsSharp.PhysicsSystem PhysicsSystem = null!;
  public BodyInterface BodyInterface => PhysicsSystem.BodyInterface!;
  public Dictionary<Entity, JoltBodyWrapper> Bodies { get; private set; } = [];

  public JoltProgram() {
    if (!Foundation.Init(false)) {
      return;
    }

    Foundation.SetTraceHandler(Logger.Info);

    Foundation.SetAssertFailureHandler((inExpression, inMessage, inFile, inLine) => {
      string message = inMessage ?? inExpression;

      string outMessage = $"[JoltPhysics] Assertion failure at {inFile}:{inLine}: {message}";

      Logger.Error(outMessage);

      throw new Exception(outMessage);
    });

    JoltSettings = new() {
      MaxBodies = MaxBodies,
      MaxBodyPairs = MaxBodyPairs,
      MaxContactConstraints = MaxContactConstraints,
      NumBodyMutexes = NumBodyMutexes,
    };
    CreateFilters();

    JobSystem = new JobSystemThreadPool();
    PhysicsSystem = new(JoltSettings);

    // ContactListener
    // PhysicsSystem.OnContactValidate += OnContactValidate;
    PhysicsSystem.OnContactAdded += OnContactAdded;
    PhysicsSystem.OnContactPersisted += OnContactPersisted;
    PhysicsSystem.OnContactRemoved += OnContactRemoved;
    // BodyActivationListener
    PhysicsSystem.OnBodyActivated += OnBodyActivated;
    PhysicsSystem.OnBodyDeactivated += OnBodyDeactivated;

    // Optional step: Before starting the physics simulation you can optimize the broad phase. This improves collision detection performance (it's pointless here because we only have 2 bodies).
    // You should definitely not call this every frame or when e.g. streaming in a new level section as it is an expensive operation.
    // Instead insert all new objects in batches instead of 1 at a time to keep the broad phase efficient.
    // PhysicsSystem.OptimizeBroadPhase();
    PhysicsSystem.Gravity *= -1;
    Logger.Info($"[GRAVITY] {PhysicsSystem.Gravity}");
  }

  public void Update() {
    Debug.Assert(JobSystem != null);
    Debug.Assert(CollisionSteps > 0);

    var result = PhysicsSystem.Update(DeltaTime, CollisionSteps, JobSystem);
    if (result != PhysicsUpdateError.None) {
      throw new Exception(result.ToString());
    }
  }

  public void Init(Span<Entity> entities) {
    foreach (var entity in entities) {
      var wrapper = new JoltBodyWrapper(BodyInterface);
      Bodies.Add(entity, wrapper);
      entity.GetRigidbody()?.Init(wrapper);
    }
  }

  public void CreateFilters() {
    ObjectLayerPairFilterTable objectLayerPairFilter = new(2);
    objectLayerPairFilter.EnableCollision(Layers.NonMoving, Layers.Moving);
    objectLayerPairFilter.EnableCollision(Layers.Moving, Layers.Moving);

    BroadPhaseLayerInterfaceTable broadPhaseLayerInterface = new(2, 2);
    broadPhaseLayerInterface.MapObjectToBroadPhaseLayer(Layers.NonMoving, BroadPhaseLayers.NonMoving);
    broadPhaseLayerInterface.MapObjectToBroadPhaseLayer(Layers.Moving, BroadPhaseLayers.Moving);

    ObjectVsBroadPhaseLayerFilterTable objectVsBroadPhaseLayerFilter = new(broadPhaseLayerInterface, 2, objectLayerPairFilter, 2);

    JoltSettings.ObjectLayerPairFilter = objectLayerPairFilter;
    JoltSettings.BroadPhaseLayerInterface = broadPhaseLayerInterface;
    JoltSettings.ObjectVsBroadPhaseLayerFilter = objectVsBroadPhaseLayerFilter;
  }

  // public static ValidateResult OnContactValidate(JoltPhysicsSharp.PhysicsSystem system, in Body body1, in Body body2, Double3 baseOffset, IntPtr collisionResult) {
  //   // Allows you to ignore a contact before it is created (using layers to not make objects collide is cheaper!)
  //   return ValidateResult.AcceptAllContactsForThisBodyPair;
  // }

  public static ValidateResult OnContactValidate(JoltPhysicsSharp.PhysicsSystem system, in Body body1, in Body body2, Double3 baseOffset, in CollideShapeResult collisionResult) {
    // Allows you to ignore a contact before it is created (using layers to not make objects collide is cheaper!)
    return ValidateResult.AcceptAllContactsForThisBodyPair;
  }

  public static void OnContactAdded(JoltPhysicsSharp.PhysicsSystem system, in Body body1, in Body body2, in ContactManifold manifold, in ContactSettings settings) {
    var data = JoltBodyWrapper.GetCollisionData(body1.ID, body2.ID);
    if (data.Item1 != null && data.Item2 != null) {
      data.Item1.GetRigidbody()?.InvokeCollision(CollisionState.Enter, data.Item2);
      data.Item2.GetRigidbody()?.InvokeCollision(CollisionState.Enter, data.Item1);
    }
  }

  public static void OnContactPersisted(JoltPhysicsSharp.PhysicsSystem system, in Body body1, in Body body2, in ContactManifold manifold, in ContactSettings settings) {
    var data = JoltBodyWrapper.GetCollisionData(body1.ID, body2.ID);
    if (data.Item1 != null && data.Item2 != null) {
      data.Item1.GetRigidbody()?.InvokeCollision(CollisionState.Stay, data.Item2);
      data.Item2.GetRigidbody()?.InvokeCollision(CollisionState.Stay, data.Item1);
    }
  }

  public static void OnContactRemoved(JoltPhysicsSharp.PhysicsSystem system, ref SubShapeIDPair subShapePair) {
    var data = JoltBodyWrapper.GetCollisionData(subShapePair.Body1ID, subShapePair.Body2ID);
    if (data.Item1 != null && data.Item2 != null) {
      data.Item1.GetRigidbody()?.InvokeCollision(CollisionState.Exit, data.Item2);
      data.Item2.GetRigidbody()?.InvokeCollision(CollisionState.Exit, data.Item1);
    }
  }

  public static void OnBodyActivated(JoltPhysicsSharp.PhysicsSystem system, in BodyID bodyID, ulong bodyUserData) {
    // Console.WriteLine("A body got activated");
  }

  public static void OnBodyDeactivated(JoltPhysicsSharp.PhysicsSystem system, in BodyID bodyID, ulong bodyUserData) {
    // Console.WriteLine("A body went to sleep");
  }

  public void Dispose() {
    foreach (var body in Bodies) {
      if (body.Key.Collected) continue;
      body.Value.Dispose();
      body.Key.GetRigidbody()?.Dispose();
    }
    JobSystem?.Dispose();
    PhysicsSystem?.Dispose();
    Foundation.Shutdown();
    GC.SuppressFinalize(this);
  }
}
