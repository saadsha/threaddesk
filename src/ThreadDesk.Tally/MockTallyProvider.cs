using System.Collections.Generic;
using System.Threading.Tasks;
using ThreadDesk.Core;

namespace ThreadDesk.Tally;

public interface ITallyConnectionManager
{
    Task<bool> TestConnectionAsync();
    Task<IEnumerable<TallyCompany>> GetCompaniesAsync();
    Task<IEnumerable<TallyLedger>> GetLedgersAsync(TallyCompany company);
    Task<IEnumerable<TallyVoucher>> GetVouchersAsync(DateTime? since = null);
    Task<bool> CreateVoucherAsync(TallyVoucher voucher);
}

public class MockTallyProvider : ITallyConnectionManager
{
    public bool ConnectionSucceeds { get; set; } = true;
    public bool ThrowOnCompanyRead { get; set; }
    public bool ThrowOnLedgerRead { get; set; }
    public IList<TallyCompany> Companies { get; } = new List<TallyCompany>
    {
        new("Shree Fabrics Pvt Ltd", "company-1"),
        new("Demo Co", "company-2")
    };
    public IDictionary<string, IList<TallyLedger>> LedgersByCompany { get; } = new Dictionary<string, IList<TallyLedger>>
    {
        ["company-1"] = new List<TallyLedger> { new("L1", "Sundry Debtors"), new("L2", "Sales") },
        ["company-2"] = new List<TallyLedger> { new("L1", "Sundry Debtors"), new("L2", "Sales") }
    };

    public Task<bool> TestConnectionAsync()
    {
        return Task.FromResult(ConnectionSucceeds);
    }

    public Task<IEnumerable<TallyCompany>> GetCompaniesAsync()
    {
        if (ThrowOnCompanyRead) throw new InvalidOperationException("Mock company retrieval failure.");
        return Task.FromResult<IEnumerable<TallyCompany>>(Companies);
    }

    public Task<IEnumerable<TallyLedger>> GetLedgersAsync(TallyCompany company)
    {
        if (ThrowOnLedgerRead) throw new InvalidOperationException("Mock ledger retrieval failure.");
        var companyId = company.Id ?? string.Empty;
        return Task.FromResult<IEnumerable<TallyLedger>>(LedgersByCompany.TryGetValue(companyId, out var ledgers)
            ? ledgers
            : Array.Empty<TallyLedger>());
    }

    public Task<IEnumerable<TallyVoucher>> GetVouchersAsync(DateTime? since = null)
    {
        var vouchers = new List<TallyVoucher> { new TallyVoucher("V1", DateTime.UtcNow.Date, "Sample", 100m) };
        return Task.FromResult<IEnumerable<TallyVoucher>>(vouchers);
    }

    public Task<bool> CreateVoucherAsync(TallyVoucher voucher)
    {
        // Mock always succeeds
        return Task.FromResult(true);
    }
}
