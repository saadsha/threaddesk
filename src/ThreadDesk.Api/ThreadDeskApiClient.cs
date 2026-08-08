using System.Net.Http;
using System.Threading.Tasks;
using ThreadDesk.Core;

namespace ThreadDesk.Api;

public interface IThreadDeskApiClient
{
    Task<bool> PingAsync();
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
}
