using System.Threading.Tasks; using ThreadDesk.Api.Models;

namespace ThreadDesk.Api.Authentication;

public interface IAuthenticationService { Task<LoginResponse?> LoginAsync(string email, string password); Task<RefreshResponse?> RefreshTokenAsync(string refreshToken); Task<MeResponse?> GetCurrentUserAsync(); Task LogoutAsync(); }

