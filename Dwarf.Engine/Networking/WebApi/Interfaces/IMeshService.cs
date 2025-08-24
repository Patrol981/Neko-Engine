using Dwarf.Networking.WebApi.Models;
using Dwarf.Rendering;

namespace Dwarf.Networking.WebApi.Interfaces;

public interface IMeshService {
  MeshResponse Get2DLevelMesh();
}