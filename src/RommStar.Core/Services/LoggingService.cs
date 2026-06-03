using RommStar.Core.Properties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.Services
{
    public class LoggingService
    {
        private static readonly string LogPath = Path.Combine(
                                    Path.GetDirectoryName(typeof(LoggingService).Assembly.Location)!,
                                    "RommStar.log");

        private static readonly object LockObject = new object();

        private static LoggingLevel _logLevel;

        public LoggingService()
        {
        }

        public void LogClear()
        {
            try
            {
                lock (LockObject)
                {
                    File.WriteAllText(LogPath, string.Empty, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to clear log file: {ex.Message}");
            }
        }

        public void Log(string message, LoggingLevel logLevel = LoggingLevel.Normal)
        {
            if ((int)logLevel > Settings.Default.LoggingLevel)
            {
                return; // Skip logging if the message's log level is higher than the configured log level
            }

            try
            {
                lock (LockObject)
                {
                    string logLine = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {message}{Environment.NewLine}";
                    File.AppendAllText(LogPath, logLine, Encoding.UTF8);
#if DEBUG
                    Debug.WriteLine($"Logged: {logLine.TrimEnd()}");
#endif
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to log to file: {ex.Message}");
            }
        }
    }
}