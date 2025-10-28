using System.Reflection;

namespace Neko.Utils;

public static class NekoPath {
  public static string AssemblyDirectory {
    get {
      try {
        return AppContext.BaseDirectory;
      } catch (Exception ex) {
        throw new Exception($"[ERROR] Failed to get assembly directory: {ex.Message}");
      }
    }
  }

  /// <summary>
  /// Gets Project Directory.
  /// <b>It is strictly made for development purposes only</b>
  /// Do not use it in production app
  /// </summary>
  public static string ProjectDirectory {
    get {
      var assembly = AssemblyDirectory;
      return Path.Combine(assembly, "../../..");
    }
  }

  public static string UserDocuments {
    get {
      string path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

      return path;
    }
  }
}