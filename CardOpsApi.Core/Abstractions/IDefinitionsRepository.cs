using System.Collections.Generic;
using System.Threading.Tasks;
using CardOpsApi.Data.Models;

namespace CardOpsApi.Core.Abstractions
{
    public interface IDefinitionRepository
    {
        Task<IList<Definition>> GetAllAsync(string? searchTerm, string? searchBy, string? type, int page, int limit);
        Task<int> GetCountAsync(string? searchTerm, string? searchBy, string? type);

        Task<Definition?> GetByIdAsync(int id);
        Task CreateAsync(Definition definition);
        Task UpdateAsync(Definition definition);
        Task DeleteAsync(int id);
    }
}
