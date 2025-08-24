namespace Dwarf.Networking.WebApi.Models;

public class MeshResponse {
  public VertexResponse[] Vertices { get; set; } = [];
  public uint[] Indices { get; set; } = [];
}