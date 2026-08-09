using System.Collections.Generic;
using System.Threading.Tasks;
using ThreadDesk.Core.Models;

namespace ThreadDesk.Core.Repositories;

public interface ILedgerRepository
{
    Task<UpsertResult> AddOrUpdateAsync(Ledger ledger);
    Task<Ledger?> GetByIdAsync(string id);
    Task<IEnumerable<Ledger>> ListByCompanyAsync(string companyId);
}
