namespace Neko.Utils;

public class Heap<T> where T : IHeapItem<T> {
  private readonly T[] _items;

  public Heap(int maxHeapSize) {
    _items = new T[maxHeapSize];
  }

  public void Add(T item) {
    item.HeapIndex = Count;
    _items[Count] = item;
    SortUp(item);
    Count += 1;
  }

  public T RemoveFirst() {
    var firstItem = _items[0];
    Count -= 1;
    _items[0] = _items[Count];
    _items[0].HeapIndex = 0;
    SortDown(_items[0]);
    return firstItem;
  }

  public bool Contains(T item) {
    return Equals(_items[item.HeapIndex], item);
  }

  public void UpdateItem(T item) {
    SortUp(item);
  }

  private void SortUp(T item) {
    var parentIndex = (item.HeapIndex - 1) / 2;
    while (true) {
      var parentItem = _items[parentIndex];
      if (item.CompareTo(parentItem) > 0) {
        Swap(item, parentItem);
      } else {
        break;
      }

      parentIndex = (item.HeapIndex - 1) / 2;
    }
  }

  private void SortDown(T item) {
    while (true) {
      var childIndexLeft = item.HeapIndex * 2 + 1;
      var childIndexRight = item.HeapIndex * 2 + 2;
      if (childIndexLeft < Count) {
        int swapIndex = childIndexLeft;
        if (childIndexRight < Count) {
          if (_items[childIndexLeft].CompareTo(_items[childIndexRight]) < 0) {
            swapIndex = childIndexRight;
          }
        }

        if (item.CompareTo(_items[swapIndex]) < 0) {
          Swap(item, _items[swapIndex]);
        } else {
          return;
        }
      } else {
        return;
      }
    }
  }

  private void Swap(T a, T b) {
    _items[a.HeapIndex] = b;
    _items[b.HeapIndex] = a;
    (b.HeapIndex, a.HeapIndex) = (a.HeapIndex, b.HeapIndex);
  }

  public int Count { get; private set; }
}