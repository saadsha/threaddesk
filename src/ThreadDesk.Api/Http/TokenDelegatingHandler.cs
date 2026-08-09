using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThreadDesk.Api.Authentication;
using ThreadDesk.Infrastructure.Security;

namespace ThreadDesk.Api.Http;

public class TokenDelegatingHandler : DelegatingHandler
{
    private readonly ICredentialStore _credentialStore;
    private readonly IAuthenticationService _authService;
    private readonly ILogger<TokenDelegatingHandler> _logger;
    private readonly string _agentVersion;

    public TokenDelegatingHandler(
        ICredentialStore credentialStore,
        IAuthenticationService authService,
        ILogger<TokenDelegatingHandler> logger,
        string agentVersion = "1.0.0")
    {
        _credentialStore = credentialStore;
        _authService = authService;
        _logger = logger;
        _agentVersion = agentVersion;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Attach X-Agent-Version header
        if (!request.Headers.Contains("X-Agent-Version"))
            request.Headers.Add("X-Agent-Version", _agentVersion);

        // Attach bearer token if present
        var cred = await _credentialStore.LoadAsync();
        if (cred?.AccessToken is not null)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", cred.AccessToken);
        }

        var response = await base.SendAsync(request, cancellationToken);

        // If unauthorized, attempt refresh and retry once
        if (response.StatusCode == HttpStatusCode.Unauthorized && cred?.RefreshToken is not null)
        {
            _logger.LogInformation("Got 401 — attempting token refresh");
            try
                        {
                            var refresh = await _authService.RefreshTokenAsync(cred.RefreshToken);
                            if (refresh is not null)
                            {
                                // retry original request with new token
                                var newCred = await _credentialStore.LoadAsync();
                                if (newCred?.AccessToken is not null)
                                {
                                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", newCred.AccessToken);

                                    // dispose previous response and retry
                                    response.Dispose();
                                    response = await base.SendAsync(CloneHttpRequestMessage(request), cancellationToken);
                                }
                            }
                            else
                            {
                                // Refresh failed — clear stored credentials to force a fresh login next time
                                _logger.LogWarning("Token refresh failed; clearing stored credentials to require a new login.");
                                try { await _credentialStore.ClearAsync(); } catch { }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Exception while attempting token refresh; clearing stored credentials to require new login.");
                            try { await _credentialStore.ClearAsync(); } catch { }
                        }
        }

        return response;
    }

    // HttpRequestMessage can't be sent twice; clone it for retry
    private static HttpRequestMessage CloneHttpRequestMessage(HttpRequestMessage req)
    {
        var clone = new HttpRequestMessage(req.Method, req.RequestUri);

        // copy request content
        if (req.Content != null)
        {
            var ms = new System.IO.MemoryStream();
            req.Content.CopyToAsync(ms).GetAwaiter().GetResult();
            ms.Position = 0;
            clone.Content = new StreamContent(ms);
            foreach (var header in req.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        // copy headers
        foreach (var header in req.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        clone.Version = req.Version;
        return clone;
    }
}