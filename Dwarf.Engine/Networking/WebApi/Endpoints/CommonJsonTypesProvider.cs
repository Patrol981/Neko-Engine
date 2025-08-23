using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Dwarf.Networking.WebApi.Models;
using Dwarf.Rendering;

namespace Dwarf.Networking.WebApi;

[JsonSerializable(typeof(VertexResponse[]))]
internal partial class VertexJsonSerializerContext : JsonSerializerContext {
}

public static class CommonJsonTypesProvider {
  public static IJsonTypeInfoResolver[] Provide() {
    return [
      VertexJsonSerializerContext.Default
    ];
  }
}