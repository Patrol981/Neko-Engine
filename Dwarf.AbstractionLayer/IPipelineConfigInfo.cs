namespace Dwarf.AbstractionLayer;

public interface IPipelineConfigInfo {
  ulong PipelineLayout { get; set; }
  ulong RenderPass { get; set; }

  IPipelineConfigInfo GetConfigInfo();
}