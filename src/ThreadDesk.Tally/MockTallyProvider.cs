using System.Collections.Generic;
using System.Threading.Tasks;
using ThreadDesk.Core;

namespace ThreadDesk.Tally;

public interface ITallyConnectionManager
{
    Task<bool> TestConnectionAsync();
    Task<IEnumerable<string>> GetCompaniesAsync();
}

public class MockTallyProvider : ITallyConnectionManager
{
    public Task<bool> TestConnectionAsync()
    {
        return Task.FromResult(true);
    }

    public Task<IEnumerable<string>> GetCompaniesAsync()
    {
        var companies = new List<string> { "Shree Fabrics Pvt Ltd", "Demo Co" };
        return Task.FromResult<IEnumerable<string>>(companies);
    }
}
