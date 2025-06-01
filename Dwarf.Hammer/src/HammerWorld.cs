using System.Numerics;
using Dwarf.Hammer.Enums;
using Dwarf.Hammer.Models;

namespace Dwarf.Hammer;

public class HammerWorld {
  internal Dictionary<BodyId, HammerObject> Bodies = [];
  internal float Gravity = 9.80665f;
  const float THRESHOLD = 0.5f;

  private Dictionary<BodyId, HammerObject> _sprites = [];
  private Dictionary<(BodyId, BodyId), bool> _contactMap = [];
  private float _dt;

  private readonly HammerInstance _hammerInstance;
  private readonly Lock _hammerWorldLock = new();
  private readonly Lock _bodiesLock = new();
  private readonly Lock _spritesLock = new();

  public HammerWorld(HammerInstance hammerInstance) {
    _hammerInstance = hammerInstance;
  }

  public Task Simulate(float dt) {
    _dt = dt;

    Dictionary<BodyId, HammerObject> snapshot;
    lock (_bodiesLock) {
      snapshot = new Dictionary<BodyId, HammerObject>(Bodies);
    }

    _sprites.Clear();
    var keys = new List<BodyId>(snapshot.Keys);
    for (int i = 0; i < keys.Count; i++) {
      var key = keys[i];
      var value = snapshot[key];
      if (value.ObjectType == ObjectType.Sprite) {
        _sprites[key] = value;
      }
    }

    // _ = ThreadPool.QueueUserWorkItem(new WaitCallback(HandleSprites!));
    Task.Run(() => HandleSprites());
    Task.Run(() => HandleGravity());
    // _ = ThreadPool.QueueUserWorkItem(HandleGravity!);

    return Task.CompletedTask;
  }

  internal void HandleGravity() {
    HammerObject[] values;

    lock (_bodiesLock) {
      values = new HammerObject[Bodies.Count];
      Bodies.Values.CopyTo(values, 0);
    }

    for (int i = 0; i < values.Length; i++) {
      var body = values[i];

      if (body.MotionType == MotionType.Dynamic) {
        HandleGrounded(body, _dt);
      }

      if (body.MotionType != MotionType.Static) {
        float x = body.Velocity.X * _dt;
        float y = body.Velocity.Y * _dt;

        body.Velocity.X -= x;
        body.Velocity.Y -= y;

        body.Position.X += x;
        body.Position.Y += y;
      }
    }
  }

  internal void HandleGravity_Old(object state) {
    foreach (var body in Bodies) {
      if (body.Value.MotionType == Enums.MotionType.Dynamic) {
        HandleGrounded(body.Value, _dt);
      }

      if (body.Value.MotionType != Enums.MotionType.Static) {
        var x = body.Value.Velocity.X * _dt;
        var y = body.Value.Velocity.Y * _dt;

        body.Value.Velocity.X -= x;
        body.Value.Velocity.Y -= y;

        body.Value.Position.X += x;
        body.Value.Position.Y += y;
      }
    }
  }

  internal void HandleGrounded(in HammerObject body, float dt) {
    if (body.Grounded) {
      if (body.Force.Y < 0) {
        body.Velocity.Y -= dt * Gravity * body.Mass;
        body.Grounded = false;
      }

      return;
    }

    if (body.Force.Y < 0) {
      body.Velocity.Y -= dt * Gravity * body.Mass;
      body.Force.Y += dt * Gravity;
    } else {
      body.Velocity.Y += dt * Gravity * body.Mass;
    }
  }

  internal void HandleSprites() {
    HammerObject[] spriteValues;
    BodyId[] spriteKeys;

    lock (_spritesLock) {
      spriteValues = new HammerObject[_sprites.Count];
      _sprites.Values.CopyTo(spriteValues, 0);

      spriteKeys = new BodyId[_sprites.Count];
      _sprites.Keys.CopyTo(spriteKeys, 0);
    }

    HammerObject[] tilemaps;
    lock (_bodiesLock) {
      var allBodies = new HammerObject[Bodies.Count];
      Bodies.Values.CopyTo(allBodies, 0);

      var tilemapList = new List<HammerObject>();
      foreach (var body in allBodies) {
        if (body.ObjectType == ObjectType.Tilemap) {
          tilemapList.Add(body);
        }
      }

      tilemaps = [.. tilemapList];
    }

    List<(BodyId, BodyId)> oldContacts;
    List<(BodyId, BodyId)> removedContacts;

    lock (_hammerWorldLock) {
      oldContacts = [.. _contactMap.Keys];
      removedContacts = [.. oldContacts.Where(x => !Bodies.ContainsKey(x.Item1))];
      oldContacts = [.. oldContacts.Except(removedContacts)];
    }

    for (int i = 0; i < spriteValues.Length; i++) {
      if (spriteValues.Length != spriteKeys.Length) continue;

      var sprite1 = spriteValues[i];
      var sprite1Id = spriteKeys[i];

      bool collidesWithAnythingGround = false;

      for (int j = 0; j < spriteValues.Length; j++) {
        if (i == j) continue;

        var sprite2 = spriteValues[j];
        if (AABB.CheckCollisionMTV(sprite1, sprite2, out var mtv)) {
          sprite1.Position += mtv;
          sprite1.Velocity = new Vector2(sprite1.Velocity.X, 0);

          lock (_hammerWorldLock) {
            var pair = (sprite1Id, spriteKeys[j]);

            if (_contactMap.TryAdd(pair, true)) {
              _hammerInstance?.OnContactAdded?.Invoke(sprite1Id, spriteKeys[j]);
            } else {
              _hammerInstance?.OnContactPersisted?.Invoke(sprite1Id, spriteKeys[j]);
            }
          }
        }
      }

      if (sprite1 != null) {
        HandleTilemaps(sprite1, tilemaps, ref collidesWithAnythingGround);
        sprite1.Grounded = collidesWithAnythingGround;
      }
    }

    List<(BodyId, BodyId)> stillThere;
    lock (_hammerWorldLock) {
      stillThere = [.. _contactMap.Keys];
      foreach (var pair in removedContacts) {
        _hammerInstance?.OnContactExit?.Invoke(pair.Item1, pair.Item2);
        _contactMap.Remove(pair);
      }
      foreach (var pair in oldContacts) {
        var g1 = Bodies.TryGetValue(pair.Item1, out var t1);
        var g2 = Bodies.TryGetValue(pair.Item2, out var t2);
        if (!g2 || g1) continue;
        var threshold = t1?.AABB.Width + t1?.AABB.Width;
        var dist = Vector2.Distance(Bodies[pair.Item1].Position, Bodies[pair.Item2].Position);
        if (dist > threshold) {
          _hammerInstance?.OnContactExit?.Invoke(pair.Item1, pair.Item2);
          _contactMap.Remove(pair);
        }
      }
      // foreach (var pair in oldContacts) {
      //   if (!stillThere.Contains(pair)) {
      //     _hammerInstance?.OnContactExit?.Invoke(pair.Item1, pair.Item2);
      //     _contactMap.Remove(pair);
      //   }
      // }
    }
  }


  internal void HandleSprites_Old() {
    HammerObject[] spriteValues;
    BodyId[] spriteKeys;

    lock (_spritesLock) {
      spriteValues = new HammerObject[_sprites.Count];
      _sprites.Values.CopyTo(spriteValues, 0);

      spriteKeys = new BodyId[_sprites.Count];
      _sprites.Keys.CopyTo(spriteKeys, 0);
    }

    HammerObject[] tilemaps;

    lock (_bodiesLock) {
      var allBodies = new HammerObject[Bodies.Count];
      Bodies.Values.CopyTo(allBodies, 0);

      List<HammerObject> tilemapList = new();
      for (int i = 0; i < allBodies.Length; i++) {
        if (allBodies[i].ObjectType == ObjectType.Tilemap) {
          tilemapList.Add(allBodies[i]);
        }
      }

      tilemaps = [.. tilemapList];
    }

    for (int i = 0; i < spriteValues.Length; i++) {
      var sprite1 = spriteValues[i];
      var sprite1Id = spriteKeys[i];

      bool collidesWithAnythingGround = false;

      for (int j = 0; j < spriteValues.Length; j++) {
        if (i == j) continue;

        var sprite2 = spriteValues[j];
        var isColl = AABB.CheckCollisionMTV(sprite1, sprite2, out var mtv);

        if (isColl) {
          if (sprite1 != null) {
            sprite1.Position += mtv;
            sprite1.Velocity.Y = 0;
          }

          lock (_hammerWorldLock) {
            var pair = (sprite1Id, spriteKeys[j]);

            if (_contactMap.TryAdd(pair, true))
              _hammerInstance?.OnContactAdded?.Invoke(sprite1Id, spriteKeys[j]);
            else
              _hammerInstance?.OnContactPersisted?.Invoke(sprite1Id, spriteKeys[j]);
          }
        }
      }

      if (sprite1 != null) {
        HandleTilemaps(sprite1, tilemaps, ref collidesWithAnythingGround);
        sprite1.Grounded = collidesWithAnythingGround;
      }
    }
  }


  internal void HandleSprites_(object state) {
    var cp = Bodies.Values.ToArray();
    var tilemaps = cp.Where(x => x.ObjectType == Enums.ObjectType.Tilemap).ToArray();
    var spriteCp = _sprites.ToArray();

    // for(int s1 = 0; s1 < _sprites.)

    foreach (var sprite1 in spriteCp) {
      var collidesWithAnythingGround = false;
      foreach (var sprite2 in spriteCp) {
        if (sprite1.Value == sprite2.Value) continue;

        var isColl = AABB.CheckCollisionMTV(sprite1.Value, sprite2.Value, out var mtv);
        if (isColl) {
          var dotProduct = Vector2.Dot(sprite1.Value.Velocity, sprite2.Value.Position);

          sprite1.Value.Position += mtv;
          sprite1.Value.Velocity.Y = 0;

          lock (_hammerWorldLock) {
            if (_contactMap.TryAdd((sprite1.Key, sprite2.Key), true)) {
              _hammerInstance?.OnContactAdded?.Invoke(sprite1.Key, sprite2.Key);
            } else {
              _hammerInstance?.OnContactPersisted?.Invoke(sprite1.Key, sprite2.Key);
            }
          }
        } else {
          // lock (_hammerWorldLock) {
          //   if (_contactMap.ContainsKey((sprite1.Key, sprite2.Key))) {
          //     _contactMap.Remove((sprite1.Key, sprite2.Key));
          //     _hammerInstance?.OnContactExit?.Invoke(sprite1.Key, sprite2.Key);
          //   }
          // }
        }
      }

      float spriteMinX = sprite1.Value.Position.X;
      float spriteMaxX = spriteMinX + sprite1.Value.AABB.Width;
      float spriteMinY = sprite1.Value.Position.Y;
      float spriteMaxY = spriteMinY + sprite1.Value.AABB.Height;

      HandleTilemaps(sprite1.Value, tilemaps, ref collidesWithAnythingGround);

      if (collidesWithAnythingGround) {
        sprite1.Value.Grounded = true;
      } else {
        sprite1.Value.Grounded = false;
      }
    }
  }

  internal static void HandleTilemaps(HammerObject sprite, ReadOnlySpan<HammerObject> tilemaps, ref bool collidesWithAnythingGround) {
    foreach (var tilemap in tilemaps) {
      var aabbss = SortOutTilemap(sprite, tilemap);
      foreach (var aabb in aabbss) {
        var isColl = AABB.CheckCollisionWithTilemapMTV(sprite.AABB, sprite.Position, aabb, tilemap.Position, out var mtv);
        if (isColl) {
          var dotProductX = Vector2.Dot(sprite.Velocity, sprite.Position);

          if (dotProductX > 0) {
            sprite.Position.X -= mtv.X;
          } else {
            sprite.Position.X += mtv.X;
          }

          if (sprite.Velocity.Y >= 0) {
            mtv.Y *= -1;
          }

          if (MathF.Abs(mtv.X) > MathF.Abs(mtv.Y)) {
            sprite.Velocity.X = 0;
            sprite.Velocity.Y = 0;
          } else {
            sprite.Velocity.Y = 0;
          }


          sprite.Position.Y += mtv.Y;

          collidesWithAnythingGround = true;
        }
      }
    }
  }

  private static ReadOnlySpan<AABB> SortOutTilemap(HammerObject sprite, HammerObject tilemap) {
    var aabbToCheck = new List<AABB>();

    foreach (var aabb in tilemap.TilemapAABBs) {
      var distance = Vector2.Distance(sprite.Position, aabb.Max);
      if (distance < THRESHOLD) {
        aabbToCheck.Add(aabb);
      }
    }

    return aabbToCheck.ToArray();
  }

  internal BodyId AddBody(Vector2 position) {
    var bodyId = new BodyId();
    var hammerObject = new HammerObject {
      Position = position
    };
    Bodies.Add(bodyId, hammerObject);
    return bodyId;
  }

  internal void RemoveBody(in BodyId bodyId) {
    Bodies.Remove(bodyId);
  }
}