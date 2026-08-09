namespace ThreadDesk.Core.Models;

public class SyncState
{
    public string Id { get; set; } = string.Empty; // composite key (e.g., company:entity)
    public string CompanyId { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? LastSyncedCursor { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = "Pending";
    public int RecordsDiscovered { get; set; }
    public int RecordsInserted { get; set; }
    public int RecordsUpdated { get; set; }
    public string? ErrorMessage { get; set; }
}
