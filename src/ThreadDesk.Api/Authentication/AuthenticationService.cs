using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThreadDesk.Api.Models;
using ThreadDesk.Infrastructure.Security;

namespace ThreadDesk.Api.Authentication;

// AuthenticationService implementation that talks to the configured API (Django backend).
// Endpoints are assumed relative to the configured ApiBaseUrl:
//  - POST auth/login      -> LoginRequest -> LoginResponse
//  - POST auth/refresh    -> RefreshRequest -> RefreshResponse
//  - GET  auth/me         -> MeResponse
//  - POST auth/logout     -> no body
public class AuthenticationService : IAuthenticationService
{
    private readonly HttpClient _http;
    private readonly IOptions<ThreadDesk.Core.AppConfig> _config;
    private readonly ICredentialStore _credentialStore;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(HttpClient http, IOptions<ThreadDesk.Core.AppConfig> config, ICredentialStore credentialStore, ILogger<AuthenticationService> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
        _logger = logger;
    }

    public async Task<LoginResponse?> LoginAsync(string email, string password)
    {
        // Send login payload with lowercase property names to match typical Django endpoints
        var payload = new { email = email, password = password };
        try
        {
            var resp = await _http.PostAsJsonAsync("auth/login", payload);
            if (!resp.IsSuccessStatusCode)
            {
                _logger?.LogWarning("Login failed with status {Status}", resp.StatusCode);
                return null;
            }

            // Read and normalize JSON to handle different API shapes (e.g. Django returns { access, refresh, user })
            var body = await resp.Content.ReadAsStringAsync();
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                static string? GetString(JsonElement elem, params string[] names)
                {
                    foreach (var n in names)
                    {
                        if (elem.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String)
                            return v.GetString();
                    }
                    return null;
                }

                var access = GetString(root, "access_token", "access");
                var refresh = GetString(root, "refresh_token", "refresh");
                var expiresAt = GetString(root, "expires_at", "expires", "token_expires_at");
                var companyId = GetString(root, "company_id", "companyId", "company");
                var userId = GetString(root, "user_id", "userId");

                if (root.TryGetProperty("user", out var userElem))
                {
                    userId = userId ?? GetString(userElem, "id", "user_id", "userId");
                    companyId = companyId ?? GetString(userElem, "company_id", "companyId", "company");
                }

                // attempt to extract name/email/plan if present
                var name = GetString(root, "name", "full_name") ?? (root.TryGetProperty("user", out var u2) ? GetString(u2, "name", "full_name") : null) ?? string.Empty;
                var emailAddr = GetString(root, "email") ?? (root.TryGetProperty("user", out var u3) ? GetString(u3, "email") : null) ?? string.Empty;
                var plan = GetString(root, "plan") ?? string.Empty;

                var login = new LoginResponse(
                    access ?? string.Empty,
                    refresh ?? string.Empty,
                    companyId ?? string.Empty,
                    userId ?? string.Empty,
                    name,
                    emailAddr,
                    plan,
                    expiresAt ?? string.Empty
                );

                var record = new CredentialRecord
                {
                    AccessToken = login.access_token,
                    RefreshToken = login.refresh_token,
                    ExpiresAt = string.IsNullOrWhiteSpace(login.expires_at) ? null : login.expires_at,
                    CompanyId = string.IsNullOrWhiteSpace(login.company_id) ? null : login.company_id,
                    UserId = string.IsNullOrWhiteSpace(login.user_id) ? null : login.user_id
                };
                await _credentialStore.SaveAsync(record);

                return login;
            }
            catch (JsonException jex)
            {
                _logger?.LogError(jex, "Failed to parse login response JSON");
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Exception during LoginAsync");
            return null;
        }
    }

    public async Task<RefreshResponse?> RefreshTokenAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return null;
        // Build a tolerant refresh payload to satisfy different backend shapes (send both possible property names)
        var payload = new { refresh = refreshToken, refresh_token = refreshToken };
        try
        {
            var resp = await _http.PostAsJsonAsync("auth/refresh", payload);
            if (!resp.IsSuccessStatusCode)
            {
                _logger?.LogWarning("Refresh token failed with status {Status}", resp.StatusCode);
                return null;
            }

            // Normalize refresh response similarly to Login endpoint (support access/refresh naming)
            var body = await resp.Content.ReadAsStringAsync();
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                static string? GetString(JsonElement elem, params string[] names)
                {
                    foreach (var n in names)
                    {
                        if (elem.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String)
                            return v.GetString();
                    }
                    return null;
                }

                var access = GetString(root, "access_token", "access");
                var refreshTokenVal = GetString(root, "refresh_token", "refresh");
                var expiresAt = GetString(root, "expires_at", "expires", "token_expires_at");

                var existing = await _credentialStore.LoadAsync();
                var record = new CredentialRecord
                {
                    AccessToken = access ?? existing?.AccessToken,
                    RefreshToken = refreshTokenVal ?? existing?.RefreshToken,
                    ExpiresAt = string.IsNullOrWhiteSpace(expiresAt) ? existing?.ExpiresAt : expiresAt,
                    CompanyId = existing?.CompanyId,
                    UserId = existing?.UserId
                };
                await _credentialStore.SaveAsync(record);

                var refresh = new RefreshResponse(access ?? string.Empty, refreshTokenVal ?? string.Empty, expiresAt ?? string.Empty);
                return refresh;
            }
            catch (JsonException jex)
            {
                _logger?.LogError(jex, "Failed to parse refresh response JSON");
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Exception during RefreshTokenAsync");
            return null;
        }
    }

    public async Task<MeResponse?> GetCurrentUserAsync()
    {
        try
        {
            var resp = await _http.GetAsync("auth/me");
            if (!resp.IsSuccessStatusCode)
            {
                _logger?.LogWarning("GetCurrentUserAsync failed with status {Status}", resp.StatusCode);
                return null;
            }

            var me = await resp.Content.ReadFromJsonAsync<MeResponse?>();
            return me;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Exception during GetCurrentUserAsync");
            return null;
        }
    }

    public async Task LogoutAsync()
    {
        try
        {
            await _http.PostAsync("auth/logout", null);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Logout request failed");
        }
        finally
        {
            try { await _credentialStore.ClearAsync(); } catch { }
        }
    }
}
