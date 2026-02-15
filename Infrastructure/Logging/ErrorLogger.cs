namespace DhanMarketData.Infrastructure.Logging;

public class ErrorLogger
{
    private readonly string _logFilePath;
    private static readonly object _lockObject = new object();

    public ErrorLogger()
    {
        var projectRoot = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.Parent?.Parent?.Parent?.FullName 
                          ?? AppDomain.CurrentDomain.BaseDirectory;
        _logFilePath = Path.Combine(projectRoot, "error_log.txt");
    }

    public void LogError(string source, string message, Exception? ex = null)
    {
        lock (_lockObject)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var logEntry = $"[{timestamp}] [{source}] {message}";
            
            if (ex != null)
            {
                logEntry += $"\nException: {ex.GetType().Name}: {ex.Message}\nStackTrace: {ex.StackTrace}";
            }
            
            logEntry += "\n" + new string('-', 80) + "\n";
            
            File.AppendAllText(_logFilePath, logEntry);
        }
    }

    public void ClearLog()
    {
        lock (_lockObject)
        {
            if (File.Exists(_logFilePath))
            {
                File.Delete(_logFilePath);
            }
        }
    }
}
