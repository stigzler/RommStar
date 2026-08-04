using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.VisualBasic;
using Microsoft.Xaml.Behaviors.Layout;
using System.Collections.ObjectModel;
using System.Windows;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace RommStar.Core.Sync
{
    /// <summary>
    /// Observable Model designed to bind directly to iNKORE Card layout components.
    /// </summary>
    public partial class PlatformSyncJob : ObservableObject
    {
        [ObservableProperty]
        private int _errorCount;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ProgressPercentage))]
        private int _processedItems;

        [ObservableProperty]
        private SyncStatus _status;

        /// <summary>
        /// Dynamic - used  in both metdata and files count feedabck to user
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ProgressPercentage))]
        private int _totalItems;

        [ObservableProperty]
        private int _romCount;

        [ObservableProperty]
        private int _warningCount;

        [ObservableProperty]
        private bool _supressSuccessLogItems;

        /// <summary>
        /// The unique identifier for this platform sync task, which is the same as the ID of the associated UI card.
        /// This allows for easy correlation between the task and its representation in the UI.
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();
        public string LaunchBoxPlatformName { get; set; } = string.Empty;
        public double ProgressPercentage => TotalItems == 0 ? 0 : ((double)ProcessedItems / TotalItems) * 100;
        public string ServerName { get; set; } = string.Empty;
        public ObservableCollection<string> SyncLogs { get; } = new();

        [RelayCommand]
        private void CopyJobLogToClipboard()
        {
            if (SyncLogs == null || SyncLogs.Count == 0)
                return;

            // Concatenate all strings in the collection with a newline separator
            string dataToCopy = string.Join(Environment.NewLine, SyncLogs);

            // Set the string to the clipboard
            Clipboard.SetText(dataToCopy);
        }

        /// <summary>
        /// Safely appends a formatted line to the log, ensuring it marshals to the LaunchBox UI thread.
        /// </summary>
        public void AddLog(string message, LogType type = LogType.Info)
        {
            if (SupressSuccessLogItems && type == LogType.Success) return;

            string prefix = type switch
            {
                LogType.Info => "ℹ️",
                LogType.Success => "✅",
                LogType.Warning => "⚠️",
                LogType.Error => "❌",
                LogType.Process => "⚙️",
                _ => "ℹ️"
            };

            string logLine = $"{prefix} [{DateTime.Now:HH:mm:ss}] {message}";

            // Safe check: If LaunchBox is shutting down or in a non-GUI test environment,
            // fallback to a direct add, otherwise marshal to the main UI thread.
            if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => SyncLogs.Add(logLine));
            }
            else
            {
                SyncLogs.Add(logLine);
            }
        }
        public enum LogType
        {
            Info,
            Success,
            Warning,
            Error,
            Process
        }
    }
}