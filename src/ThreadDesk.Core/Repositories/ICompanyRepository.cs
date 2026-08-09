using System.Threading.Tasks;
using ThreadDesk.Core.Models;
using System.Collections.Generic;

namespace ThreadDesk.Core.Repositories;

public interface ICompanyRepository
{
    Task<UpsertResult> AddOrUpdateAsync(Company company);
    Task<Company?> GetByIdAsync(string id);
    Task<IEnumerable<Company>> ListAsync();
}
