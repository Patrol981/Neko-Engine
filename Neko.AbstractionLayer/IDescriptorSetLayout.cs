namespace Neko.AbstractionLayer;

public interface IDescriptorSetLayout : IDisposable {
  public ulong GetDescriptorSetLayoutPointer();
}