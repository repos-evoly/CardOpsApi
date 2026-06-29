using CardOpsApi.Core.Dtos;
using CardOpsApi.Data.Models;

namespace CardOpsApi.Core.Abstractions
{
    public interface IManualReverseRepository
    {
        Task<ManualReverse>           CreateAsync(ManualReverse manualreverse);

        Task<IList<ManualReverseDto>> GetAllAsync(
            DateTime? fromDate  = null,
            DateTime? toDate    = null,
            string?   keyword   = null,
            string?   accountNumber = null,
            int       page      = 1,
            int       limit     = 100000);

        Task<int> GetCountAsync(
            DateTime? fromDate  = null,
            DateTime? toDate    = null,
            string?   keyword   = null,
            string?   accountNumber = null);


        Task<ManualReverse?>  GetByIdAsync(int id);
        Task UpdateStatusAsync(int id, string status, string? reference, string? message);
    }
}