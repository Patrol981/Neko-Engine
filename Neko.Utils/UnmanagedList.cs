namespace Neko.Utils;

[System.Serializable]
public unsafe struct Node<T> {
  public T Data;
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
  public Node<T>* Next;
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type

#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
  public static unsafe Node<T>* CreateNode(T data) {
    Node<T> newNode = new();
    newNode.Data = data;
    newNode.Next = null;
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
    return &newNode;
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
  }
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type

#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
  public static void Insert(Node<T>* head, T data) {
    var newNode = CreateNode(data);
    newNode->Next = head;
    head = newNode;
  }
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
}

public unsafe class UnmanagedList<T> {
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
  private Node<T>* _head = null!;
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type

  public void Add(T data) {
    if (_head == null) {
      _head = Node<T>.CreateNode(data);
    } else {
      Node<T>.Insert(_head, data);
    }
  }

  public T? GetAtIndex(int index) {
    int i = 0;
    var current = _head;
    while (current != null) {
      if (i == index) {
        return current->Data;
      }
      current = current->Next;
      i++;
    }
    var notFound = new Node<T>();
    return notFound.Data;
  }
}
