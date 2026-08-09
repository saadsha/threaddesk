using System.Threading.Tasks;

namespace ThreadDesk.Infrastructure.Security;

public class CredentialRecord
{
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public string? ExpiresAt { get; set; }
    public string? CompanyId { get; set; }
    public string? UserId { get; set; }
}

public interface ICredentialStore
{
    Task SaveAsync(CredentialRecord record);
    Task<CredentialRecord?> LoadAsync();
    Task ClearAsync();
}