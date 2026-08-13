using CommunityToolkit.Mvvm.ComponentModel;
using RommStar.Core.Dtos.Romm;
using System;
using System.Collections.Generic;
using System.Text;
using System.Web;

namespace RommStar.Core.Sync
{
    // 1. Must be a partial class inheriting from ObservableObject
    public partial class RomQueueItem : ObservableObject
    {
        // =========================================================
        // DYNAMIC PROPERTIES (These notify the UI when changed)
        // =========================================================

        [ObservableProperty]
        private int _retryCount = 0;

        [ObservableProperty]
        private bool _isQuarantined = false;

        [ObservableProperty]
        private string _lastError = string.Empty;

        [ObservableProperty]
        private RomQueueItemStatus _status = RomQueueItemStatus.Queued;

        // =========================================================
        // STATIC PROPERTIES (No UI notification needed after creation)
        // =========================================================

        public DateTime AddedAt { get; set; } = DateTime.Now;

        public string GameNameSanitised { get; set; } = string.Empty;

        public bool IsMultiFileGame { get; set; } = false;

        public bool IsPriority { get; set; } = false;

        public bool IsSiblingSet { get; set; } = false;
        public bool IsSingleFileGame => !IsMultiFileGame && !IsSiblingSet;

        public string LaunchboxId { get; set; } = string.Empty;

        public string MasterFilename { get; set; } = string.Empty;

        public List<RomFileDTO>? MultiFiles { get; set; } = new();

        public bool NotifyLaunchboxOnCompletion { get; set; } = false;

        public string PlatformName { get; set; } = string.Empty;

        public string PlatformStub { get; set; } = string.Empty;

        public List<int> RommIds { get; set; } = new();

        public string ServerId { get; set; } = string.Empty;

        public long TotalSizeBytes { get; set; }


        /// <summary>
        /// Safely updates the status, respecting terminal error states.
        /// </summary>
        public void UpdateQueueItemStatus(RomQueueItemStatus romQueueItemStatus)
        {
            // Note: We use the capital 'Status' here so it hits the generated 
            // property and triggers the UI notification event!
            if (this.Status != RomQueueItemStatus.Errored &&
                this.Status != RomQueueItemStatus.CompleteWithWarnings)
            {
                this.Status = romQueueItemStatus;
            }
        }

        public string ToCsv()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"[{GameNameSanitised} ({PlatformName})] <{MasterFilename}>: ");
            if (IsMultiFileGame) sb.Append("Is Multifile, ");
            if (IsSiblingSet) sb.Append("Is Sibling Set, ");
            if (IsSingleFileGame) sb.Append("Is Single File, ");
            if (IsPriority) sb.Append("Is Priority, ");

            if (IsQuarantined)
            {
                sb.Append("IS QUARANTINED, ");  
                sb.Append($"Retry Count: {RetryCount}, ");
            }

            sb.Append($"RomIds: [{String.Join(",", RommIds)}], ");
            sb.Append($"ServerID: [{ServerId}], ");
            sb.Append($"Number of Multifiles: {MultiFiles.Count}, ");
            sb.Append($"Last Error: [{LastError}]");

            return sb.ToString();
        }
    }
}