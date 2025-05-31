namespace Dwarf.AbstractionLayer;

public interface IDescriptorSetLayout : IDisposable {
  public ulong GetDescriptorSetLayoutPointer();
}