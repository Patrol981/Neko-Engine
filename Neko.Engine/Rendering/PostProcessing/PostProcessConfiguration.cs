namespace Neko.Rendering.PostProcessing;

public struct PostProcessConfiguration {
  public PostProcessingConfigurationFlag FlagIdentifier { get; set; }
  public string VertexName { get; set; }
  public string FragmentName { get; set; }
}