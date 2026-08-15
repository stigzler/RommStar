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

        public void HeadLog()
        {
            // Verbatim string literal to preserve the exact ASCII art formatting and backslashes
            string header = Environment.NewLine +
        @" /$$$$$$$                          /$$      /$$  /$$$$$$   /$$                        
| $$__  $$                        | $$$    /$$$ /$$__  $$ | $$                        
| $$  \ $$  /$$$$$$  /$$$$$$/$$$$ | $$$$  /$$$$| $$  \__//$$$$$$    /$$$$$$   /$$$$$$ 
| $$$$$$$/ /$$__  $$| $$_  $$_  $$| $$ $$/$$ $$|  $$$$$$|_  $$_/   |____  $$ /$$__  $$
| $$__  $$| $$  \ $$| $$ \ $$ \ $$| $$  $$$| $$ \____  $$ | $$      /$$$$$$$| $$  \__/
| $$  \ $$| $$  | $$| $$ | $$ | $$| $$\  $ | $$ /$$  \ $$ | $$ /$$ /$$__  $$| $$      
| $$  | $$|  $$$$$$/| $$ | $$ | $$| $$ \/  | $$|  $$$$$$/ |  $$$$/|  $$$$$$$| $$      
|__/  |__/ \______/ |__/ |__/ |__/|__/     |__/ \______/   \___/   \_______/|__/      
                                                                                      
																			by stigzler" + Environment.NewLine
            + $"Log started: {DateTime.Now}" + Environment.NewLine
            + "(Use Python Syntax highlighting and monospaced font in Notepad++ to make reading easier) " + Environment.NewLine
            + "-----------------------------------------------------------------------------------------" + Environment.NewLine;

            try
            {
                lock (LockObject)
                {
                    File.AppendAllText(LogPath, header, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to write HeadLog to file: {ex.Message}");
            }
        }

        public void Log(string message, LoggingLevel logLevel = LoggingLevel.Normal)
        {
            if (logLevel > _settingsService.Settings.LoggingLevel)
            {
                return; // Skip logging if the message's log level is higher than the configured log level
            }

            // Hardcoded settings for calling member formatting
            bool showCallingMember = _settingsService.Settings.LoggingIncludeMember;
            bool prependCallingMember = _settingsService.Settings.LoggingPrependMember;

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
            stringBuilder.Append($"{DateTime.Now:HH:mm:ss.fff} ");

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
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to log to file: {ex.Message}");
            }
        }


        public void LogUnhandledException(string source, Exception ex)
        {
            try
            {
                // 1. Unpack wrapper exceptions
                Exception rootException = ex;
                if (ex is AggregateException aggEx && aggEx.InnerException != null)
                {
                    rootException = aggEx.InnerException;
                }
                else if (ex.InnerException != null && (ex.Message.Contains("A Task's exception(s) were not observed") || ex.Message.Contains("One or more errors occurred")))
                {
                    rootException = ex.InnerException;
                }

                // 2. Drill into the StackTrace
                string originDetails = string.Empty;
                var stackTrace = new StackTrace(rootException, true);

                StackFrame? targetFrame = null;
                foreach (var frame in stackTrace.GetFrames())
                {
                    var method = frame.GetMethod();
                    if (method?.DeclaringType != null)
                    {
                        string namespaceName = method.DeclaringType.Namespace ?? string.Empty;
                        if (namespaceName.StartsWith("RommStar", StringComparison.OrdinalIgnoreCase))
                        {
                            targetFrame = frame;
                            break;
                        }
                    }
                }

                targetFrame ??= stackTrace.GetFrame(0);

                if (targetFrame != null)
                {
                    var methodInfo = targetFrame.GetMethod();
                    if (methodInfo != null)
                    {
                        var className = methodInfo.DeclaringType?.Name ?? "UnknownClass";
                        var methodName = methodInfo.Name;

                        // Handle Compiler-Generated Lambdas & Closures
                        if (methodName.StartsWith("<") && methodName.Contains(">"))
                        {
                            int startIndex = methodName.IndexOf('<') + 1;
                            int endIndex = methodName.IndexOf('>');
                            if (endIndex > startIndex)
                            {
                                methodName = methodName.Substring(startIndex, endIndex - startIndex);
                            }

                            if (className.StartsWith("<") && methodInfo.DeclaringType?.DeclaringType != null)
                            {
                                className = methodInfo.DeclaringType.DeclaringType.Name;
                            }
                        }
                        // Handle Async State Machines
                        else if (methodName == "MoveNext" && methodInfo.DeclaringType != null)
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

                        originDetails = $"[{className}.{methodName}]";
                    }
                }

                // Call the local Log method directly!
                Log($"UNHANDLED EXCEPTION {originDetails} [{source}]: {rootException.Message}", LoggingLevel.Normal);
                Log($"StackTrace:\n{rootException.StackTrace}", LoggingLevel.Verbose);
            }
            catch (Exception logEx)
            {
                Debug.WriteLine($"Critical error inside exception logger: {logEx.Message}");
            }
        }
    }

}


