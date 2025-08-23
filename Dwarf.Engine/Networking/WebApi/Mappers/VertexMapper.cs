using Dwarf.Networking.WebApi.Models;
using Dwarf.Rendering;

namespace Dwarf.Networking.WebApi.Mappers;

public static class VertexMapper {
  public static VertexResponse ToVertexResponse(this Vertex vertex) {
    return new VertexResponse() {
      Position = [
        vertex.Position.X,
        vertex.Position.Y,
        vertex.Position.Z
      ],
      Color = [
        vertex.Color.X,
        vertex.Color.Y,
        vertex.Color.Z
      ],
      Normal = [
        vertex.Normal.X,
        vertex.Normal.Y,
        vertex.Normal.Z
      ],
      Uv = [
        vertex.Uv.X,
        vertex.Uv.Y,
      ],
    };
  }

  public static VertexResponse[] ToVertexResponseArray(this Vertex[] vertices) {
    return [.. vertices.Select(x => x.ToVertexResponse())];
  }
}