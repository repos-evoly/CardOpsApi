using System.Collections.Generic;
using System.Threading.Tasks;
using CardOpsApi.Data.Models;

namespace CardOpsApi.Core.Abstractions
{
    public interface ICurrencyRepository
    {
        Task<IList<Currency>> GetAllAsync(string? searchTerm, string? searchBy, int page, int limit);
        Task<Currency?> GetByIdAsync(int id);
        Task CreateAsync(Currency currency);
        Task UpdateAsync(Currency currency);
        Task DeleteAsync(int id);
        Task<int> GetCountAsync(string? searchTerm, string? searchBy);

    }
}
