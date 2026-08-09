namespace ThreadDesk.Api.Models;

public record LoginRequest(string Email, string Password);

public record LoginResponse(
    string access_token,
    string refresh_token,
    string company_id,
    string user_id,
    string name,
    string email,
    string plan,
    string expires_at // parse as needed
);

public record RefreshRequest(string refresh_token);

public record RefreshResponse(string access_token, string refresh_token, string expires_at);

public record MeResponse(
    string company_id,
    string user_id,
    string name,
    string email,
    string plan,
    string token_expires_at
);