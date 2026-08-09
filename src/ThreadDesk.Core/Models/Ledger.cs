namespace ThreadDesk.Core.Models;

public class Ledger
{
    // Local, deterministic key composed from CompanyId and ExternalId.
    public string Id { get; set; } = string.Empty;
    public string CompanyId { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? GroupName { get; set; }
    public string? Metadata { get; set; }
    public string? UpdatedAt { get; set; }
}
