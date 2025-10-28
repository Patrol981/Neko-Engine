namespace Neko.Extensions.Lists;

public class PublicList<T> {
  private T[] _data;
  private int _capacity;

  private readonly Lock _lock = new();

  public PublicList(int initialCapacity = 8) {
    if (initialCapacity < 1) initialCapacity = 1;
    _capacity = initialCapacity;
    _data = new T[initialCapacity];
  }

  public int Size { get; private set; } = 0;
  public bool IsEmpty { get { return Size == 0; } }

  public T GetAt(int index) {
    lock (_lock) {
      ThrowIfIndexOutOfRange(index);
      return _data[index];
    }
  }

  public void SetAt(T newElement, int index) {
    lock (_lock) {
      ThrowIfIndexOutOfRange(index);
      _data[index] = newElement;
    }
  }

  public void InsertAt(T newElement, int index) {
    lock (_lock) {
      ThrowIfIndexOutOfRange(index);
      if (Size == _capacity) {
        Resize();
      }

      for (int i = Size; i > index; i--) {
        _data[i] = _data[i - 1];
      }

      _data[index] = newElement;
      Size++;
    }
  }

  public void DeleteAt(int index) {
    lock (_lock) {
      ThrowIfIndexOutOfRange(index);
      for (int i = index; i < Size - 1; i++) {
        _data[i] = _data[i + 1];
      }

      _data[Size - 1] = default(T)!;
      Size--;
    }
  }

  public void Add(T newElement) {
    lock (_lock) {
      if (Size == _capacity) {
        Resize();
      }

      _data[Size] = newElement;
      Size++;
    }
  }

  public bool Contains(T value) {
    for (int i = 0; i < Size; i++) {
      T currentValue = _data[i];
      if (currentValue!.Equals(value)) {
        return true;
      }
    }
    return false;
  }

  public void Clear() {
    _data = new T[_capacity];
    Size = 0;
  }

  private void Resize() {
    lock (_lock) {
      T[] resized = new T[_capacity * 2];
      for (int i = 0; i < _capacity; i++) {
        resized[i] = _data[i];
      }
      _data = resized;
      _capacity = _capacity * 2;
    }
  }

  private void ThrowIfIndexOutOfRange(int index) {
    if (index > Size - 1 || index < 0) {
      throw new ArgumentOutOfRangeException(string.Format("The current size of the array is {0}", Size));
    }
  }

  public T[] GetData() => _data;
}