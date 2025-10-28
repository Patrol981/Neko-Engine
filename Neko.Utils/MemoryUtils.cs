using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Neko.Extensions.Logging;

namespace Neko.Utils;

public static class MemoryUtils {
  public static bool IsNull(byte[] bytes) {
    bool isNull = true;
    for (int i = 0; i < bytes.Length; i++) {
      if (bytes[i] != 0) {
        isNull = false;
        break;
      }
    }
    return isNull;
  }

  public static unsafe void MemCopy(nint destination, nint source, int byteCount) {
    if (byteCount <= 0) {
      throw new Exception("ByteCount is NULL");
    }

    if (byteCount > 2130702268) {
      throw new Exception("ByteCount is too big");
    }

    System.Buffer.MemoryCopy((void*)source, (void*)destination, byteCount, byteCount);
  }

  public static unsafe void MemCopy(void* destination, void* source, int byteCount) {
    if (byteCount <= 0) {
      throw new Exception("ByteCount is NULL");
    }

    if (byteCount > 2130702268) {
      throw new Exception("ByteCount is too big");
    }

    System.Buffer.MemoryCopy(source, destination, byteCount, byteCount);
  }

  public static void MemCopy(ref byte src, ref byte dst, uint byteCount) {
    Unsafe.CopyBlock(ref dst, ref src, byteCount);
  }

  /*
  public static IntPtr ToIntPtr<T>(T[] arr) where T : struct {
    int size = Unsafe.SizeOf<T>();
    IntPtr ptr = IntPtr.Zero;
    try {
      ptr = Marshal.AllocHGlobal(size * arr.Length);
      for (int i = 0; i < arr.Length; i++) {
        Marshal.StructureToPtr(arr[i], IntPtr.Add(ptr, i * size), true);
      }
    } catch {
      if (ptr != IntPtr.Zero) {
        Marshal.FreeHGlobal(ptr);
      }
      throw;
    }
    return ptr;
  }
  */

  public static IntPtr ObjectToPtr<T>(T obj) {
    if (obj == null) throw new NullReferenceException(nameof(obj));

    int size = Unsafe.SizeOf<T>();
    IntPtr ptr = IntPtr.Zero;

    try {
      ptr = Marshal.AllocHGlobal(size);
      Marshal.StructureToPtr(obj, ptr, true);
    } catch {
      if (ptr != IntPtr.Zero) {
        Marshal.FreeHGlobal(ptr);
      }
      throw;
    }
    return ptr;
  }

  public static IntPtr ToIntPtr<T>(T data) where T : struct {
    int size = Unsafe.SizeOf<T>();
    IntPtr ptr = IntPtr.Zero;
    try {
      ptr = Marshal.AllocHGlobal(size);
      Marshal.StructureToPtr(data, ptr, true);
    } catch {
      if (ptr != IntPtr.Zero) {
        Marshal.FreeHGlobal(ptr);
      }
      throw;
    }
    return ptr;
  }

  public static void FreeIntPtr<T>(IntPtr ptr) {
    try {
      Logger.Info($"Freeing {typeof(T)} - {ptr}");
      Marshal.FreeHGlobal(ptr);
    } catch {
      throw;
    }
  }

  public static nint AllocateMemory(int size) {
    return Marshal.AllocHGlobal(size);
  }

#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
  public static unsafe T* AllocateMemory<T>(int count) where T : struct {
    return (T*)Marshal.AllocHGlobal(Unsafe.SizeOf<T>() * count);
  }
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type

  public static T? FromIntPtr<T>(nint ptr) {
    var obj = Marshal.PtrToStructure<T>(ptr);
    Marshal.FreeHGlobal(ptr);
    return obj;
  }
}