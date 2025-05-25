namespace Dwarf.AbstractionLayer;

public interface IPipeline : IDisposable {
  public void Bind(nint commandBuffer);
}