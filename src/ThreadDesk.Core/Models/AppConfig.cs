namespace ThreadDesk.Core;

public class AppConfig
{
    public string ApiBaseUrl { get; set; } = "https://yourapp.com/api";
    public int SyncIntervalMinutes { get; set; } = 30;
    public string AgentVersion { get; set; } = "1.0.0";
}
