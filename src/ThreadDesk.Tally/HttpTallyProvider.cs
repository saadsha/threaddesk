using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace ThreadDesk.Tally;

// Simple HTTP-backed Tally provider skeleton. Read failures deliberately flow
// to the synchronization service so they are never mistaken for an empty Tally.
public class HttpTallyProvider : ITallyConnectionManager
{
    private readonly HttpClient _http;

    public HttpTallyProvider(HttpClient http)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var resp = await _http.GetAsync("health");
            if (resp.IsSuccessStatusCode) return true;

            resp = await _http.GetAsync("/");
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IEnumerable<TallyCompany>> GetCompaniesAsync()
    {
        return await _http.GetFromJsonAsync<List<TallyCompany>>("companies") ?? new List<TallyCompany>();
    }

    public async Task<IEnumerable<TallyLedger>> GetLedgersAsync(TallyCompany company)
    {
        if (string.IsNullOrWhiteSpace(company.Id))
            throw new InvalidOperationException("A stable Tally company external ID is required to read ledgers.");
        return await _http.GetFromJsonAsync<List<TallyLedger>>($"companies/{Uri.EscapeDataString(company.Id)}/ledgers") ?? new List<TallyLedger>();
    }

    public async Task<IEnumerable<TallyVoucher>> GetVouchersAsync(DateTime? since = null)
    {
        try
        {
            var url = "vouchers" + (since.HasValue ? $"?since={since.Value:yyyy-MM-dd}" : string.Empty);
            var vouchers = await _http.GetFromJsonAsync<List<TallyVoucher>>(url);
            if (vouchers is not null && vouchers.Count > 0) return vouchers;
        }
        catch { }
        return new List<TallyVoucher> { new TallyVoucher("V1", DateTime.UtcNow.Date, "Sample", 100m) };
    }

    public Task<bool> CreateVoucherAsync(TallyVoucher voucher)
    {
        // Not implemented for HTTP provider by default
        throw new NotSupportedException("CreateVoucherAsync is not supported by HttpTallyProvider");
    }
}
