using System.Collections.Generic;
using System.Threading.Tasks;
using CardOpsApi.Data.Models;

namespace CardOpsApi.Core.Abstractions
{
    public interface IReasonRepository
    {
        Task<IList<Reason>> GetAllAsync(string? searchTerm, string? searchBy, int page, int limit);

        Task<int> GetCountAsync(string? searchTerm, string? searchBy);

        Task<Reason?> GetByIdAsync(int id);
        Task CreateAsync(Reason reason);
        Task UpdateAsync(Reason reason);
        Task DeleteAsync(int id);
    }
}
