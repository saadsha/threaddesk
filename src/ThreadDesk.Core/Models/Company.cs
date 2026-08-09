namespace ThreadDesk.Core.Models;

public class Company
{
    // Stable identifier provided by Tally. It is also the local primary key.
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Metadata { get; set; }
}
