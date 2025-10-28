using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Neko.Networking.WebApi.Models;
using Neko.Rendering;

namespace Neko.Networking.WebApi;

[JsonSerializable(typeof(VertexResponse[]))]
[JsonSerializable(typeof(MeshResponse[]))]
internal partial class MeshJsonSerializerContext : JsonSerializerContext {
}

public static class CommonJsonTypesProvider {
  public static IJsonTypeInfoResolver[] Provide() {
    return [
      MeshJsonSerializerContext.Default,
    ];
  }
}