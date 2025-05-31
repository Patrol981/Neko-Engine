namespace Dwarf.AbstractionLayer;

public interface IStorageCollection : IDisposable {
  void CreateStorage(
    IDevice device,
    DescriptorType descriptorType,
    BufferUsage bufferUsage,
    int arraySize,
    ulong bufferSize,
    ulong bufferCount,
    IDescriptorSetLayout layout,
    IDescriptorPool pool,
    string storageName,
    ulong offsetAlignment,
    bool mapWholeBuffer
  );

  void CheckSize(
    string key,
    int index,
    int elemCount,
    IDescriptorSetLayout layout,
    bool mapWholeBuffer
  );

  void WriteBuffer(
    string key,
    int index,
    nint data,
    ulong size
  );

  ulong GetDescriptor(
    string key,
    int index
  );
}