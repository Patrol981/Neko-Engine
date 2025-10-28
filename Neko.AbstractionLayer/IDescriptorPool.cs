namespace Neko.AbstractionLayer;

public interface IDescriptorPool : IDisposable {
  public ulong GetHandle();
}