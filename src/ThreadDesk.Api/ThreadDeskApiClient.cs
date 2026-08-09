using System.Net.Http;
using System.Threading.Tasks;
using ThreadDesk.Core;

namespace ThreadDesk.Api;

public interface IThreadDeskApiClient
{
    Task<bool> PingAsync();
    Task<string?> GetCurrentUserAsync();
}

public class ThreadDeskApiClient : IThreadDeskApiClient
{
    private readonly HttpClient _client;
    private readonly AppConfig _config;

    public ThreadDeskApiClient(HttpClient client)
    {
        _client = client;
    }

    public async Task<bool> PingAsync()
    {
        // Simple health check - implementation to be expanded
        var resp = await _client.GetAsync("/api/health");
        return resp.IsSuccessStatusCode;
    }

    public async Task<string?> GetCurrentUserAsync()
    {
        // Calls auth/me which requires authentication; returns raw JSON string or null on failure
        var resp = await _client.GetAsync("auth/me");
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadAsStringAsync();
    }
}
