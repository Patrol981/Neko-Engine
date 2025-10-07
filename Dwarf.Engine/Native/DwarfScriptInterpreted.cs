using Dwarf.EntityComponentSystem;

namespace Dwarf.Native;

public delegate void DwarfScriptEvent();

public class DwarfScriptInterpreted : DwarfScript {
  internal DwarfScriptEvent? OnAwake;
  internal DwarfScriptEvent? OnStart;
  internal DwarfScriptEvent? OnUpdate;

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