using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ThreadDesk.Infrastructure.Security;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Load ApiBaseUrl from agent appsettings.json if present, else use arg or default
        var baseUrl = "https://beta.xlapparals.in/api/";
        var configPath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "..\\src\\ThreadDesk.Agent", "appsettings.json");
        try
        {
            if (System.IO.File.Exists(configPath))
            {
                using var fs = System.IO.File.OpenRead(configPath);
                using var doc = JsonDocument.Parse(fs);
                if (doc.RootElement.TryGetProperty("AppConfig", out var app) && app.TryGetProperty("ApiBaseUrl", out var url))
                {
                    baseUrl = url.GetString() ?? baseUrl;
                }
            }
        }
        catch { }

        Console.Write("Email: ");
        var email = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(email)) { Console.WriteLine("Email required"); return 1; }
        // Read password securely (mask input)
        Console.Write("Password: ");
        var pwd = ReadPassword();
        Console.WriteLine();
        if (string.IsNullOrEmpty(pwd)) { Console.WriteLine("Password required"); return 1; }
        var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
        var payload = new { email = email, password = pwd };
        try
        {
            var resp = await client.PostAsJsonAsync("auth/login", payload);
            if (!resp.IsSuccessStatusCode)
            {
                Console.WriteLine($"Login failed: {resp.StatusCode}");
                return 2;
            }

            var body = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            string? access = null; string? refresh = null; string? userId = null; string? companyId = null;

            if (root.TryGetProperty("access", out var a)) access = a.GetString();
            if (root.TryGetProperty("access_token", out var a2)) access = a2.GetString();
            if (root.TryGetProperty("refresh", out var r)) refresh = r.GetString();
            if (root.TryGetProperty("refresh_token", out var r2)) refresh = r2.GetString();

            if (root.TryGetProperty("user", out var u))
            {
                if (u.TryGetProperty("id", out var uid)) userId = uid.GetString();
                if (u.TryGetProperty("company", out var cid)) companyId = cid.GetString();
                if (u.TryGetProperty("company_id", out var cid2)) companyId = cid2.GetString();
            }

            // Persist securely via DPAPI-backed store
            var store = new DpapiCredentialStore();
            var record = new ThreadDesk.Infrastructure.Security.CredentialRecord
            {
                AccessToken = access,
                RefreshToken = refresh,
                UserId = userId,
                CompanyId = companyId,
                ExpiresAt = null
            };
            await store.SaveAsync(record);
            Console.WriteLine("Login successful — credentials saved to the local DPAPI store.");

            // Call /auth/me with the access token to verify
            if (!string.IsNullOrEmpty(access))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);
                var meResp = await client.GetAsync("auth/me");
                Console.WriteLine($"GET auth/me -> {(int)meResp.StatusCode} {meResp.StatusCode}");
                if (meResp.IsSuccessStatusCode)
                {
                    var meBody = await meResp.Content.ReadAsStringAsync();
                    Console.WriteLine("auth/me returned success — user info saved to credential store (no secrets printed).");
                }
            }

            // Test refresh endpoint using both field names
            if (!string.IsNullOrEmpty(refresh))
            {
                var refreshPayload = new { refresh = refresh, refresh_token = refresh };
                var refreshResp = await client.PostAsJsonAsync("auth/refresh", refreshPayload);
                Console.WriteLine($"POST auth/refresh -> {(int)refreshResp.StatusCode} {refreshResp.StatusCode}");
                if (refreshResp.IsSuccessStatusCode)
                {
                    Console.WriteLine("Refresh succeeded — credential store updated.");
                    var refreshBody = await refreshResp.Content.ReadAsStringAsync();
                    using var rd = JsonDocument.Parse(refreshBody);
                    var rroot = rd.RootElement;
                    string? newAccess = null; string? newRefresh = null;
                    if (rroot.TryGetProperty("access", out var na)) newAccess = na.GetString();
                    if (rroot.TryGetProperty("access_token", out var na2)) newAccess = na2.GetString();
                    if (rroot.TryGetProperty("refresh", out var nr)) newRefresh = nr.GetString();
                    if (rroot.TryGetProperty("refresh_token", out var nr2)) newRefresh = nr2.GetString();

                    var existing = await store.LoadAsync();
                    var updated = new ThreadDesk.Infrastructure.Security.CredentialRecord
                    {
                        AccessToken = newAccess ?? existing?.AccessToken,
                        RefreshToken = newRefresh ?? existing?.RefreshToken,
                        CompanyId = existing?.CompanyId,
                        UserId = existing?.UserId,
                        ExpiresAt = existing?.ExpiresAt
                    };
                    await store.SaveAsync(updated);
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception: {ex.Message}");
            return 99;
        }
    }

    static string ReadPassword()
    {
        var pass = string.Empty;
        ConsoleKeyInfo key;
        do
        {
            key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Backspace && pass.Length > 0)
            {
                pass = pass[0..^1];
            }
            else if (!char.IsControl(key.KeyChar))
            {
                pass += key.KeyChar;
            }
        } while (key.Key != ConsoleKey.Enter);
        return pass;
    }
}
