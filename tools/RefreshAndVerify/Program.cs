using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ThreadDesk.Infrastructure.Security;

class Program
{
    static async Task<int> Main()
    {
        var configPath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "..\\src\\ThreadDesk.Agent", "appsettings.json");
        var baseUrl = "https://beta.xlapparals.in/api/";
        try {
            if (System.IO.File.Exists(configPath))
            {
                using var fs = System.IO.File.OpenRead(configPath);
                using var doc = JsonDocument.Parse(fs);
                if (doc.RootElement.TryGetProperty("AppConfig", out var app) && app.TryGetProperty("ApiBaseUrl", out var url))
                    baseUrl = url.GetString() ?? baseUrl;
            }
        } catch { }

        var store = new DpapiCredentialStore();
        var cred = await store.LoadAsync();
        if (cred == null)
        {
            Console.WriteLine("No credentials found in DPAPI store at C:\\ProgramData\\ThreadDesk\\credentials.bin");
            return 1;
        }

        using var client = new HttpClient { BaseAddress = new Uri(baseUrl) };

        // Try GET auth/me with current access token
        if (!string.IsNullOrEmpty(cred.AccessToken))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cred.AccessToken);
            var meResp = await client.GetAsync("auth/me");
            Console.WriteLine($"GET auth/me -> {(int)meResp.StatusCode} {meResp.StatusCode}");
            if (meResp.IsSuccessStatusCode)
            {
                Console.WriteLine("auth/me succeeded with existing access token. Credentials are valid.");
                return 0;
            }
            else
            {
                Console.WriteLine("auth/me returned non-success; will attempt refresh if refresh token present.");
            }
        }

        if (string.IsNullOrEmpty(cred.RefreshToken))
        {
            Console.WriteLine("No refresh token available to attempt refresh.");
            return 2;
        }

        // Attempt refresh
        var payload = new { refresh = cred.RefreshToken, refresh_token = cred.RefreshToken };
        var refreshResp = await client.PostAsJsonAsync("auth/refresh", payload);
        Console.WriteLine($"POST auth/refresh -> {(int)refreshResp.StatusCode} {refreshResp.StatusCode}");
        var refreshBody = await refreshResp.Content.ReadAsStringAsync();

        // Sanitize body by removing access/refresh values before printing
        try
        {
            using var doc = JsonDocument.Parse(refreshBody);
            var root = doc.RootElement;
            using var ms = new System.IO.MemoryStream();
            using var writer = new Utf8JsonWriter(ms);
            SanitizeAndWrite(root, writer);
            writer.Flush();
            var sanitized = System.Text.Encoding.UTF8.GetString(ms.ToArray());
            Console.WriteLine("Refresh response (sanitized):");
            Console.WriteLine(sanitized);
        }
        catch
        {
            Console.WriteLine("Refresh response body could not be parsed as JSON. (Not shown for security reasons)");
        }

        if (!refreshResp.IsSuccessStatusCode)
        {
                    Console.WriteLine("Refresh failed; clearing stored credentials to require a fresh login.");
                    try { await store.ClearAsync(); } catch { }
                    return 3;
                }

        // Parse tokens and save
        try
        {
            using var doc = JsonDocument.Parse(refreshBody);
            var root = doc.RootElement;
            string? newAccess = null, newRefresh = null;
            if (root.TryGetProperty("access", out var a)) newAccess = a.GetString();
            if (root.TryGetProperty("access_token", out var a2)) newAccess = a2.GetString();
            if (root.TryGetProperty("refresh", out var r)) newRefresh = r.GetString();
            if (root.TryGetProperty("refresh_token", out var r2)) newRefresh = r2.GetString();

            var updated = new ThreadDesk.Infrastructure.Security.CredentialRecord
            {
                AccessToken = newAccess ?? cred.AccessToken,
                RefreshToken = newRefresh ?? cred.RefreshToken,
                CompanyId = cred.CompanyId,
                UserId = cred.UserId,
                ExpiresAt = cred.ExpiresAt
            };
            await store.SaveAsync(updated);
            Console.WriteLine("Credential store updated with refreshed tokens (values not printed). Attempting auth/me with new access token...");

            if (!string.IsNullOrEmpty(updated.AccessToken))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", updated.AccessToken);
                var me2 = await client.GetAsync("auth/me");
                Console.WriteLine($"GET auth/me -> {(int)me2.StatusCode} {me2.StatusCode}");
                if (me2.IsSuccessStatusCode)
                {
                    Console.WriteLine("auth/me succeeded after refresh. Credentials persisted correctly.");
                    return 0;
                }
            }

            Console.WriteLine("auth/me still failed after refresh.");
            return 4;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception while parsing refresh response: {ex.Message}");
            return 5;
        }
    }

    static void SanitizeAndWrite(JsonElement elem, Utf8JsonWriter writer)
    {
        switch (elem.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var prop in elem.EnumerateObject())
                {
                    if (string.Equals(prop.Name, "access", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(prop.Name, "access_token", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(prop.Name, "refresh", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(prop.Name, "refresh_token", StringComparison.OrdinalIgnoreCase))
                    {
                        writer.WriteString(prop.Name, "<redacted>");
                    }
                    else
                    {
                        writer.WritePropertyName(prop.Name);
                        SanitizeAndWrite(prop.Value, writer);
                    }
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in elem.EnumerateArray()) SanitizeAndWrite(item, writer);
                writer.WriteEndArray();
                break;
            default:
                elem.WriteTo(writer);
                break;
        }
    }
}
