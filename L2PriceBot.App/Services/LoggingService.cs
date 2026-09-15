using System;
using System.IO;
using System.Threading.Tasks;

namespace L2PriceBot.App.Services;

public class LoggingService
{
    private readonly string _logPath;
    
    public LoggingService()
    {
        _logPath = Path.Combine(Environment.CurrentDirectory, "logs", "bot.log");
        var directory = Path.GetDirectoryName(_logPath);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public async Task LogAuditAsync(string username, string action, string item, string detail)
    {
        var time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var logLine = $"[{time}] USER: {username} | ACTION: {action} | ITEM: {item} | DETAIL: {detail}\n";
        
        try
        {
            await File.AppendAllTextAsync(_logPath, logLine);
            Console.WriteLine(logLine.TrimEnd());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error writing log: {ex.Message}");
        }
    }
}
