using System.Collections.Generic;
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
    var sprites = Bodies.Values.ToArray();
    var indices = Bodies.Keys.ToArray();
    var tilemapList = new List<HammerObject>();
    lock (_spritesLock) {
      for (int i = 0; i < sprites.Length; i++) {
        if (sprites[i].ObjectType != ObjectType.Tilemap) continue;

        tilemapList.Add(sprites[i]);
      }
    }

    GetOldContacts(out var oldContacts);
    GetRemovedContacts(oldContacts, Bodies, out var removedContacts);

    for (int i = 0; i < sprites.Length; i++) {
      if (sprites.Length != indices.Length) continue;

      var sprite1 = sprites[i];
      var sprite1Id = indices[i];

      bool collidesWithAnythingGround = false;

      for (int j = 0; j < sprites.Length; j++) {
        if (i == j) continue;

        var sprite2 = sprites[j];
        var sprite2Id = indices[j];
        if (AABB.CheckCollisionMTV(sprite1, sprite2, out var mtv)) {
          if (!sprite2.IsTrigger && !sprite1.IsTrigger) {
            sprite1.Position += mtv;
            sprite1.Velocity = new Vector2(sprite1.Velocity.X, 0);
          }

          lock (_hammerWorldLock) {
            var pair = (sprite1Id, sprite2Id);

            if (_contactMap.TryAdd(pair, true)) {
              _hammerInstance?.OnContactAdded?.Invoke(sprite1Id, sprite2Id);
            } else {
              _hammerInstance?.OnContactPersisted?.Invoke(sprite1Id, sprite2Id);
            }
          }
        }
      }

      if (sprite1 != null && !sprite1.IsTrigger) {
        HandleTilemaps(sprite1, tilemapList.ToArray(), ref collidesWithAnythingGround);
        sprite1.Grounded = collidesWithAnythingGround;
      }
    }


    lock (_hammerWorldLock) {
      GetStillThereBodies(out var stillThere);
      for (int i = 0; i < removedContacts.Count; i++) {
        _hammerInstance?.OnContactExit?.Invoke(removedContacts[i].Item1, removedContacts[i].Item2);
        _contactMap.Remove(removedContacts[i]);
      }
      for (int i = 0; i < oldContacts.Count; i++) {
        var g1 = Bodies.TryGetValue(oldContacts[i].Item1, out var t1);
        var g2 = Bodies.TryGetValue(oldContacts[i].Item2, out var t2);
        if (!g2 || g1) continue;
        var threshold = t1?.AABB.Width + t1?.AABB.Width;
        var dist = Vector2.Distance(Bodies[oldContacts[i].Item1].Position, Bodies[oldContacts[i].Item2].Position);
        if (dist > threshold) {
          _hammerInstance?.OnContactExit?.Invoke(oldContacts[i].Item1, oldContacts[i].Item2);
          _contactMap.Remove(oldContacts[i]);
        }
      }
    }
  }

  private void GetOldContacts(
    out List<(BodyId, BodyId)> oldContacts
  ) {
    oldContacts = [];
    for (int i = 0; i < _contactMap.Keys.Count; i++) {
      var target = _contactMap.Keys.ElementAtOrDefault(i);
      oldContacts.Add(target);
    }
  }

  private void GetRemovedContacts(
    in List<(BodyId, BodyId)> oldContacts,
    in Dictionary<BodyId, HammerObject> inBodies,
    out List<(BodyId, BodyId)> removedContacts
  ) {
    removedContacts = [];
    for (int i = 0; i < oldContacts.Count; i++) {
      var target = oldContacts.ElementAtOrDefault(i);
      if (inBodies.ContainsKey(target.Item1)) {
        removedContacts.Add(target);
      }
    }
  }

  private void GetStillThereBodies(out List<(BodyId, BodyId)> stillThere) {
    stillThere = [];
    for (int i = 0; i < _contactMap.Keys.Count; i++) {
      stillThere.Add(_contactMap.Keys.ElementAtOrDefault(i));
    }
  }

  internal void HandleSpritesLinq() {
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
          if (!sprite2.IsTrigger && !sprite1.IsTrigger) {
            sprite1.Position += mtv;
            sprite1.Velocity = new Vector2(sprite1.Velocity.X, 0);
          }

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

      if (sprite1 != null && !sprite1.IsTrigger) {
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