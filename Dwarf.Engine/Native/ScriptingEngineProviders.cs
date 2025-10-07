using Lua;

namespace Dwarf.Native;

public static class ScritpingEngineProviders {
  private static LuaState? s_lua;

  public static LuaState ProvideLua() {
    s_lua ??= LuaState.Create();
    return s_lua;
  }
}