using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Neko.Utils;

public class UnmanagedArray<T> where T : struct {
  public IntPtr Handle { get; private set; }
  public int Length { get; private set; }

  public T this[int i] {
    get {
      var maxSize = Unsafe.SizeOf<T>() * Length;
      var inputSize = Unsafe.SizeOf<T>() * i;
      return inputSize > maxSize
        ? throw new ArgumentOutOfRangeException(paramName: nameof(inputSize))
        : (T)Marshal.PtrToStructure(Handle + i * Unsafe.SizeOf<T>(), typeof(T))!;
    }

    set {
      var maxSize = Unsafe.SizeOf<T>() * Length;
      var inputSize = Unsafe.SizeOf<T>() * i;
      if (inputSize > maxSize) {
        throw new ArgumentOutOfRangeException(paramName: nameof(inputSize));
      }

      // WARN: Potential memory leak
      Marshal.StructureToPtr(value, Handle + i * Unsafe.SizeOf<T>(), false);
    }
  }

  public UnmanagedArray(T[] array) {
    Length = array.Length;
    var size = Unsafe.SizeOf<T>() * Length;
    Handle = Marshal.AllocHGlobal(size);
  }

  ~UnmanagedArray() {
    Marshal.FreeHGlobal(Handle);
  }
}
