using CardOpsApi.Core.Dtos;
using CardOpsApi.Data.Models;

namespace CardOpsApi.Core.Abstractions
{
    public interface IFisBankRecordRepository
    {
        Task<FisBankRecordImportResultDto> ImportFromExcelAsync(
            Stream excelStream,
            string fileName);

        Task SoftDeleteAsync(int id);

        Task<IList<FisBankRecord>> GetAllAsync(int page, int limit);
        Task<int> GetCountAsync();
        Task<FisBankRecord?> GetByIdAsync(int id);
        Task UpdateAsync(FisBankRecord record);

        Task<IEnumerable<FisBankFileDto>> GetFilesAsync(
            string? search = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null);

        Task<FisBankFileDto?> GetFileAsync(int id);

        Task<IList<FisBankRecord>> GetAllByFileIdAsync(int fileId);

        Task<IList<FisBankRecordDto>> GetFileRecordsAsync(int fileId);
        Task<bool> CanImportFileAsync(string fileName);
    }
}