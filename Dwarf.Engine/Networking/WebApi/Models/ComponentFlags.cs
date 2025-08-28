namespace Dwarf.Networking.WebApi.Models;

[Flags]
public enum ComponentFlags {
  Empty = 0,
  Entity = 1,
  Network = 1 << 1,
  Transform = 1 << 2,
  SpriteRenderer = 1 << 3,
  MeshRenderer = 1 << 4,
  Tilemap = 1 << 5
}