using System.Collections.Generic;
using System.Threading.Tasks;
using CardOpsApi.Data.Models;

namespace CardOpsApi.Core.Abstractions
{
    public interface ITransactionRepository
    {
        Task<IList<Transactions>> GetAllAsync(string? searchTerm, string? searchBy, string? type, int page, int limit);
        Task<int> GetCountAsync(string? searchTerm, string? searchBy, string? type);

        Task<Transactions?> GetByIdAsync(int id);
        Task CreateAsync(Transactions transaction);
        Task UpdateAsync(Transactions transaction);
        Task DeleteAsync(int id);
        Task<(int atmCount, int posCount, decimal totalPosAmount, decimal totalAtmAmount)> GetStatsAsync();
        Task<List<(string AtmAccount, int RefundCount)>> GetTopRefundAtmsAsync();
    }
}
