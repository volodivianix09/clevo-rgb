namespace ClevoRgbLite.Services;

public static class AppLogger
{
    private static readonly object Gate = new();

    public static string DirectoryPath { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClevoRGBLite");

    public static string LogPath => Path.Combine(DirectoryPath, "app.log");

    public static void Log(string message, Exception? exception = null)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(DirectoryPath);
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
                if (exception is not null)
                {
                    line += Environment.NewLine + exception;
                }
                File.AppendAllText(LogPath, line + Environment.NewLine);
            }
        }
        catch
        {
            // Logging must never crash the app.
        }
    }
}
