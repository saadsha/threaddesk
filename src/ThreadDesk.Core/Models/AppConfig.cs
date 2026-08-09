namespace ThreadDesk.Core;

public class AppConfig
{
    public string ApiBaseUrl { get; set; } = "https://yourapp.com/api";
    public string TallyBaseUrl { get; set; } = "http://localhost:9000/";
    // ODBC connection string to connect to Tally (if using ODBC provider)
    public string? TallyOdbcConnectionString { get; set; }
    // Optional SQL queries to map Tally schema to models. If provided, these will be used by the ODBC provider.
    public string? TallyCompaniesQuery { get; set; }
    public string? TallyLedgersQuery { get; set; }
    public string? TallyVouchersQuery { get; set; }
    public int TallyOdbcCommandTimeoutSeconds { get; set; } = 30;
    // Optional stable Tally company ID to synchronize. When omitted, every returned company is synchronized.
    public string? TallyCompanyExternalId { get; set; }

    public int SyncIntervalMinutes { get; set; } = 30;
    public string AgentVersion { get; set; } = "1.0.0";
}
