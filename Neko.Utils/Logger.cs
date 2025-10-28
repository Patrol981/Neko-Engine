namespace Neko.Extensions.Logging;

public static class Logger {
  public static void Info(object message) {
    WriteColored(ConsoleColor.Green, "[INFO]");
    Console.WriteLine(" " + message);
  }

  public static void Warn(object message) {
    WriteColored(ConsoleColor.Yellow, "[WARN]");
    Console.WriteLine(" " + message);
  }

  public static void Error(object message) {
    WriteColored(ConsoleColor.Red, "[ERROR]");
    Console.WriteLine(" " + message);
  }

  public static void NoLabel(object message, ConsoleColor consoleColor = ConsoleColor.White) {
    WriteColored(consoleColor, message);
    Console.WriteLine();
  }

  private static void WriteColored(ConsoleColor color, object message) {
    Console.ForegroundColor = color;
    Console.Write(message);
    Console.ForegroundColor = ConsoleColor.White;
  }
}