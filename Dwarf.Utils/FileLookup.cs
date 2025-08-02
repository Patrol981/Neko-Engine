namespace Dwarf.Utils;

public static class FileLookup {
  public static string? FindPathOfAFile(string exactFileName) {
    var startingDirectoryString = DwarfPath.AssemblyDirectory;

    var filePath = HandleDirectory(startingDirectoryString, "Resources", exactFileName);
    return filePath;
  }

  private static string? HandleDirectory(
    in string currentPath,
    in string directoryName,
    in string targetFileName
  ) {
    var targetPath = Path.Combine(currentPath, directoryName);
    var existPath = Path.Combine(targetPath, targetFileName);
    if (File.Exists(existPath)) {
      return existPath;
    }

    var dirs = Directory.GetDirectories(targetPath);
    foreach (var dir in dirs) {
      return HandleDirectory(targetPath, dir, targetFileName);
    }

    return null;
  }
}