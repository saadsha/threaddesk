using System;
using System.Threading.Tasks;
using ThreadDesk.Infrastructure.Security;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var store = new DpapiCredentialStore();
        if (args.Length == 1 && args[0].Equals("load", StringComparison.OrdinalIgnoreCase))
        {
            var rec = await store.LoadAsync();
            if (rec == null)
            {
                Console.WriteLine("No credentials found in store.");
                return 1;
            }
            var outJson = System.Text.Json.JsonSerializer.Serialize(rec, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine("Loaded CredentialRecord:");
            Console.WriteLine(outJson);
            return 0;
        }

        if (args.Length < 2)
        {
            Console.WriteLine("Usage: SaveCredentials <access> <refresh> [userId] [companyId] [expiresAt]  OR SaveCredentials load");
            return 1;
        }

        var access = args[0];
        var refresh = args[1];
        var userId = args.Length > 2 ? args[2] : null;
        var companyId = args.Length > 3 ? args[3] : null;
        var expiresAt = args.Length > 4 ? args[4] : null;

        var record = new CredentialRecord
        {
            AccessToken = access,
            RefreshToken = refresh,
            UserId = userId,
            CompanyId = companyId,
            ExpiresAt = expiresAt
        };

        await store.SaveAsync(record);
        Console.WriteLine("Saved credentials to the DPAPI-backed store (C:/ProgramData/ThreadDesk/credentials.bin).");
        return 0;
    }
}
