using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace ThreadDesk.Infrastructure.Security;

public class DpapiCredentialStore : ICredentialStore
{
    private readonly string _filePath;

    public DpapiCredentialStore()
    {
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var dir = Path.Combine(programData, "ThreadDesk");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "credentials.bin");
    }

    public async Task SaveAsync(CredentialRecord record)
    {
        var json = JsonSerializer.Serialize(record);
        var plain = Encoding.UTF8.GetBytes(json);
        var protectedBytes = ProtectedData.Protect(plain, null, DataProtectionScope.LocalMachine);
        await File.WriteAllBytesAsync(_filePath, protectedBytes);
    }

    public async Task<CredentialRecord?> LoadAsync()
    {
        if (!File.Exists(_filePath)) return null;
        try
        {
            var protectedBytes = await File.ReadAllBytesAsync(_filePath);
            var plain = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.LocalMachine);
            var json = Encoding.UTF8.GetString(plain);
            var record = JsonSerializer.Deserialize<CredentialRecord>(json);
            return record;
        }
        catch
        {
            return null;
        }
    }

    public Task ClearAsync()
    {
        try { if (File.Exists(_filePath)) File.Delete(_filePath); } catch { }
        return Task.CompletedTask;
    }
}