namespace Dwarf.Native;

public interface IScriptEngine : IDisposable {
  void Execute(string code);
}