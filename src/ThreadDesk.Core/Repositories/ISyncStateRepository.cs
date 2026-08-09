using System.Threading.Tasks;
using ThreadDesk.Core.Models;
using System.Collections.Generic;

namespace ThreadDesk.Core.Repositories;

public interface ISyncStateRepository
{
    Task UpsertAsync(SyncState state);
    Task<SyncState?> GetByIdAsync(string id);
    Task<IEnumerable<SyncState>> ListByCompanyAsync(string companyId);
}
