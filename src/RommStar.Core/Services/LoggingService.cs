using RommStar.Core.Primitives;
using RommStar.Core.Properties;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace RommStar.Core.Services
{
    public class LoggingService
    {
        private readonly SettingsService _settingsService;

        private static readonly string LogPath = Path.Combine(
                                    Path.GetDirectoryName(typeof(LoggingService).Assembly.Location)!,
                                    "RommStar.log");

        private static readonly object LockObject = new object();

        private static LoggingLevel _logLevel;

        public LoggingService(SettingsService settingsService)
        {
            _settingsService = settingsService;
        }

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
            if (logLevel > _settingsService.Settings.LoggingLevel)
            {
                return; // Skip logging if the message's log level is higher than the configured log level
            }

            // Hardcoded settings for calling member formatting
            bool showCallingMember = true;
            bool prependCallingMember = true;

            string methodDetails = string.Empty;

            if (showCallingMember && !string.IsNullOrEmpty(message))
            {
                var stackTrace = new StackTrace();
                // GetFrame(1) grabs the caller of this Log method
                var frame = stackTrace.GetFrame(1);
                var methodInfo = frame?.GetMethod();

                if (methodInfo != null)
                {
                    var className = methodInfo.ReflectedType?.Name ?? "UnknownClass";
                    var methodName = methodInfo.Name;

                    // Detect async state machine
                    if (methodName == "MoveNext" && methodInfo.DeclaringType != null)
                    {
                        var stateMachineType = methodInfo.DeclaringType;
                        var parentType = stateMachineType.DeclaringType;
                        if (parentType != null)
                        {
                            var originalMethod = parentType
                                .GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                                .FirstOrDefault(m =>
                                    m.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType == stateMachineType);

                            if (originalMethod != null)
                            {
                                methodName = originalMethod.Name;
                                className = parentType.Name;
                            }
                        }
                    }

                    methodDetails = $"[{className}.{methodName}]";
                }
            }

            // Build the string line
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} ");

            if (prependCallingMember)
            {
                if (!string.IsNullOrEmpty(methodDetails))
                {
                    stringBuilder.Append($"{methodDetails} ");
                }
                stringBuilder.Append($"{message}");
            }
            else
            {
                stringBuilder.Append($"{message} ");
                if (!string.IsNullOrEmpty(methodDetails))
                {
                    stringBuilder.Append($"{methodDetails}");
                }
            }

            stringBuilder.Append(Environment.NewLine);
            string logLine = stringBuilder.ToString();

            try
            {
                lock (LockObject)
                {
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