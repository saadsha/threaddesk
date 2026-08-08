using System.Threading.Tasks;
using ThreadDesk.Tally;
using Xunit;

namespace ThreadDesk.UnitTests;

public class TallyMockTests
{
    [Fact]
    public async Task MockTally_Returns_Companies()
    {
        var mock = new MockTallyProvider();
        var ok = await mock.TestConnectionAsync();
        var companies = await mock.GetCompaniesAsync();

        Assert.True(ok);
        Assert.NotEmpty(companies);
    }
}
