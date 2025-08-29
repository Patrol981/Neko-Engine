using System.Collections.Concurrent;

namespace Dwarf.EntityComponentSystemLegacy;

public class ComponentManager {
  private ConcurrentDictionary<Type, Component> _components;

  public ComponentManager() {
    _components = [];
  }

  public void AddComponent(Component component) {
    _components[component.GetType()] = component;
  }

  public T GetComponent<T>() where T : Component {
    var component = _components!.TryGetValue(typeof(T), out var value);
    return component ? (T)value! : null!;
  }

  public void RemoveComponent<T>() where T : Component {
    _components.Remove(typeof(T), out _);
  }

  public ConcurrentDictionary<Type, Component> GetAllComponents() {
    return _components;
  }
}