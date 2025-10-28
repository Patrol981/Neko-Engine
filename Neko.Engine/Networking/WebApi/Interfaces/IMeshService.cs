using Neko.Networking.WebApi.Models;
using Neko.Rendering;

namespace Neko.Networking.WebApi.Interfaces;

public interface IMeshService {
  MeshResponse Get2DLevelMesh();
}