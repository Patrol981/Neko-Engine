using Lua;

namespace Neko.Native;

public class LuaEngine : IScriptEngine {
  private readonly LuaState _lua;

  public LuaEngine() {
    _lua = ScritpingEngineProviders.ProvideLua();
  }

  public void Execute(string code) {
    _lua.DoStringAsync(code).AsTask().Wait();
  }

  public async Task ExecuteAsync(string code, CancellationToken? cancellationToken) {
    await _lua.DoStringAsync(code, cancellationToken: cancellationToken ?? default);
  }


  public void Dispose() {
    GC.SuppressFinalize(this);
  }
}