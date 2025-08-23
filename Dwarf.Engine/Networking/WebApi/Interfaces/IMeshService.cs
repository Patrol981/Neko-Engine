using Dwarf.Networking.WebApi.Models;
using Dwarf.Rendering;

namespace Dwarf.Networking.WebApi.Interfaces;

public interface IMeshService {
  VertexResponse[] Get2DLevelMesh();
}