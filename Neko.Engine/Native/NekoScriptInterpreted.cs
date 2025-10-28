using Neko.EntityComponentSystem;

namespace Neko.Native;

public delegate void NekoScriptEvent();

public class NekoScriptInterpreted : NekoScript {
  internal NekoScriptEvent? OnAwake;
  internal NekoScriptEvent? OnStart;
  internal NekoScriptEvent? OnUpdate;

  public string UpdateCode { get; init; } = string.Empty;
  public string StartCode { get; init; } = string.Empty;
  public string AwakeCode { get; init; } = string.Empty;

  internal IScriptEngine? ScriptEngine { get; set; }

  public static string ReadFile(string path) {
    try {
      var @code = File.ReadAllText(path);

      return @code;
    } catch {
      throw;
    }
  }

  public override void Dispose() {
    base.Dispose();
    GC.SuppressFinalize(this);
  }
}