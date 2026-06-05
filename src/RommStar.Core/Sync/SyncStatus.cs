namespace RommStar.Core.Sync
{
    public enum SyncStatus
    {
        Queued,
        ProcessingMetadata,
        SyncingFiles,
        Completed,
        CompletedWithWarnings,
        CompletedWithErrors,
        Cancelled
    }
}