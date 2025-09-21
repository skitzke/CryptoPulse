using System.Text;

namespace CryptoPulse.Helpers;

public static class Log
{
    // Just dumping logs to Desktop so I can always find them quickly while testing.
    private static readonly string LogFile =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "cryptopulse.log");

    // Expose the path so I can open the log from anywhere (UI / diagnostics).
    public static string LogPath => LogFile;

    // Shortcut for info logs.
    public static void Info(string msg)  => Write("INFO", msg);

    // Shortcut for errors (string).
    public static void Error(string msg) => Write("ERROR", msg);

    // Shortcut for errors (exception object, optional message).
    public static void Error(Exception ex, string msg = "") => Write("ERROR", $"{msg}\n{ex}");

    // Core write method → timestamp + level + message.
    private static void Write(string level, string msg)
    {
        try
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {level} {msg}{Environment.NewLine}";
            File.AppendAllText(LogFile, line, Encoding.UTF8);

            // Try to also write to console (but MAUI WinExe often has no console).
            try { Console.WriteLine(line); } catch { /* ignore */ }
        }
        catch
        {
            // If even logging fails, ignore it — app must not crash because of logging.
        }
    }
}
