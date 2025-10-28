namespace Neko.Native;

public interface IScriptEngine : IDisposable {
  void Execute(string code);
}