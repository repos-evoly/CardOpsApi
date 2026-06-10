using AutoMapper;
using CardOpsApi.Core.Abstractions;
using CardOpsApi.Core.Dtos;
using CardOpsApi.Data.Context;
using CardOpsApi.Data.Models;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace CardOpsApi.Core.Repositories
{
    public class FisBankRecordRepository : IFisBankRecordRepository
    {
        private readonly CardOpsApiDbContext _context;
        private readonly IMapper _mapper;

        public FisBankRecordRepository(CardOpsApiDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<FisBankRecordImportResultDto> ImportFromExcelAsync(Stream excelStream, string fileName)
        {
            var result = new FisBankRecordImportResultDto();
            var importedRecords = new List<FisBankRecord>();

            // Reuse existing file record if it already exists
            var fisBankFile = await _context.FisBankFiles
                .FirstOrDefaultAsync(x => x.FileName == fileName);

            if (fisBankFile == null)
            {
                fisBankFile = new FisBankFile { FileName = fileName };
                await _context.FisBankFiles.AddAsync(fisBankFile);
                await _context.SaveChangesAsync();
            }

            // Build a lookup of records already stored for this file, keyed by MerchantNo.
            // IgnoreQueryFilters so soft-deleted records are included — a re-import should
            // restore them rather than create a duplicate.
            var existingByMerchantNo = await _context.FisBankRecords
                .IgnoreQueryFilters()
                .Where(r => r.FisBankFileId == fisBankFile.Id)
                .ToDictionaryAsync(r => r.MerchantNo ?? string.Empty);

            using var workbook = new XLWorkbook(excelStream);
            var worksheet = workbook.Worksheets.First();
            var rows = worksheet.RowsUsed().Skip(1); // skip header row

            foreach (var row in rows)
            {
                try
                {
                    // Skip completely empty rows
                    if (row.CellsUsed().Count() == 0)
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    // Skip rows without Merchant Number
                    var merchantNo = row.Cell(1).GetValue<string>()?.Trim();
                    if (string.IsNullOrWhiteSpace(merchantNo))
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    if (existingByMerchantNo.TryGetValue(merchantNo, out var existing))
                    {
                        // UPDATE: overwrite every data field from the spreadsheet.
                        // Reset transfer state so the record can be re-processed.
                        // Restore IsDeleted in case the record had been soft-deleted.
                        existing.MerchantName      = row.Cell(2).GetValue<string>()?.Trim();
                        existing.BankingAccountNo  = row.Cell(3).GetValue<string>()?.Trim()?[2..]; //ignores the first two chars from the account number
                        existing.BankName          = row.Cell(4).GetValue<string>()?.Trim();
                        existing.BranchName        = row.Cell(5).GetValue<string>()?.Trim();
                        existing.NetAmount         = ParseDecimal(row.Cell(6)) ?? 0m;
                        existing.ProcessingDate    = ParseDate(row.Cell(7)) ?? DateTimeOffset.MinValue;
                        existing.DiscountRate      = ParseDecimal(row.Cell(8)) ?? 0m;
                        existing.TotalAmount       = ParseDecimal(row.Cell(9)) ?? 0m;
                        existing.TrxNo             = ParseInt(row.Cell(10));
                        existing.Owner             = row.Cell(11).GetValue<string>()?.Trim();
                        existing.TransferStatus    = "PENDING";
                        existing.TransferReference = null;
                        existing.FailureReason     = null;
                        existing.IsDeleted         = false;
                        importedRecords.Add(existing);
                    }
                    else
                    {
                        // INSERT: first time this MerchantNo appears in this file
                        var record = new FisBankRecord
                        {
                            MerchantNo        = merchantNo,
                            MerchantName      = row.Cell(2).GetValue<string>()?.Trim(),
                            BankingAccountNo  = row.Cell(3).GetValue<string>()?.Trim(),
                            BankName          = row.Cell(4).GetValue<string>()?.Trim(),
                            BranchName        = row.Cell(5).GetValue<string>()?.Trim(),
                            NetAmount         = ParseDecimal(row.Cell(6)) ?? 0m,
                            ProcessingDate    = ParseDate(row.Cell(7)) ?? DateTimeOffset.MinValue,
                            DiscountRate      = ParseDecimal(row.Cell(8)) ?? 0m,
                            TotalAmount       = ParseDecimal(row.Cell(9)) ?? 0m,
                            TrxNo             = ParseInt(row.Cell(10)),
                            Owner             = row.Cell(11).GetValue<string>()?.Trim(),
                            FisBankFileId     = fisBankFile.Id,
                            TransferStatus    = "PENDING",
                            TransferReference = null,
                            FailureReason     = null,
                            IsDeleted         = false
                        };
                        await _context.FisBankRecords.AddAsync(record);
                        importedRecords.Add(record);
                    }
                }
                catch (Exception ex)
                {
                    result.SkippedCount++;
                    result.Errors.Add($"Row {row.RowNumber()}: {ex.Message}");
                }
            }

            // EF change-tracking detects mutations on existing entities automatically;
            // new entities were explicitly Added above — one SaveChanges covers both.
            await _context.SaveChangesAsync();

            result.ImportedCount   = importedRecords.Count;
            result.ImportedRecords = importedRecords.Select(r => _mapper.Map<FisBankRecordDto>(r)).ToList();

            // Update file-level summary
            if (importedRecords.Count > 0)
            {
                fisBankFile.ImportedCount = importedRecords.Count;
                fisBankFile.FirstRecordId = importedRecords.Min(x => x.Id);
                fisBankFile.LastRecordId  = importedRecords.Max(x => x.Id);
                await _context.SaveChangesAsync();
            }

            return result;
        }        public async Task SoftDeleteAsync(int id)
        {
            var record = await _context.FisBankRecords.FindAsync(id);
            if (record != null)
            {
                record.IsDeleted = true;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IList<FisBankRecord>> GetAllAsync(int page, int limit)
        {
            return await _context.FisBankRecords
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * limit)
                .Take(limit)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<int> GetCountAsync()
        {
            return await _context.FisBankRecords.CountAsync();
        }

        public async Task<FisBankRecord?> GetByIdAsync(int id)
        {
            return await _context.FisBankRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task UpdateAsync(FisBankRecord record)
        {
            _context.FisBankRecords.Update(record);
            await _context.SaveChangesAsync();
        }

        // --- helpers ---
        private static decimal? ParseDecimal(IXLCell cell)
        {
            try
            {
                var raw = cell.GetString();
                if (string.IsNullOrWhiteSpace(raw)) return null;
                return decimal.TryParse(raw, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null;
            }
            catch { return null; }
        }

        private static int? ParseInt(IXLCell cell)
        {
            try
            {
                var raw = cell.GetString();
                if (string.IsNullOrWhiteSpace(raw)) return null;
                return int.TryParse(raw, out var i) ? i : null;
            }
            catch { return null; }
        }

        private static DateTimeOffset? ParseDate(IXLCell cell)
        {
            try
            {
                var raw = cell.GetString();
                if (string.IsNullOrWhiteSpace(raw)) return null;
                if (DateTime.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var dt))
                    return (DateTimeOffset)dt;
                return null;
            }
            catch { return null; }
        }
        public async Task<IEnumerable<FisBankFileDto>> GetFilesAsync(
            string? search = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null)
        {
            var query = _context.FisBankFiles.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(f => f.FileName.Contains(search));

            if (dateFrom.HasValue)
                query = query.Where(f => f.CreatedAt >= dateFrom.Value);

            if (dateTo.HasValue)
                query = query.Where(f => f.CreatedAt <= dateTo.Value.AddDays(1));

            return await query
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new FisBankFileDto { Id = x.Id,
                    FileName = x.FileName,
                    ImportedCount = x.ImportedCount,
                    FirstRecordId = x.FirstRecordId,
                    LastRecordId = x.LastRecordId,
                    CreatedAt = x.CreatedAt })
                .ToListAsync();
        }

        public async Task<FisBankFileDto?> GetFileAsync(int id)
        {
            return await _context.FisBankFiles
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new FisBankFileDto
                {
                    Id = x.Id,
                    FileName = x.FileName,
                    ImportedCount = x.ImportedCount,
                    FirstRecordId = x.FirstRecordId,
                    LastRecordId = x.LastRecordId,
                    CreatedAt = x.CreatedAt,


                })
                .FirstOrDefaultAsync();
        }

        public async Task<IList<FisBankRecordDto>> GetFileRecordsAsync(int fileId)
        {
            var records = await _context.FisBankRecords
                .AsNoTracking()
                .Where(x => x.FisBankFileId == fileId)
                .OrderBy(x => x.Id)
                .ToListAsync();

            return records
                .Select(x => _mapper.Map<FisBankRecordDto>(x))
                .ToList();
        }

        public async Task<IList<FisBankRecord>> GetAllByFileIdAsync(int fileId)
        {
            return await _context.FisBankRecords
                .Where(r => r.FisBankFileId == fileId)
                .OrderBy(r => r.Id)
                .AsNoTracking()
                .ToListAsync();
        }
        public async Task<bool> CanImportFileAsync(string fileName)
        {
            var fileIds = await _context.FisBankFiles
                .AsNoTracking()
                .Where(f => f.FileName == fileName)
                .Select(f => f.Id)
                .ToListAsync();

            if (!fileIds.Any()) return true; // first import – allow

            var hasSuccessful = await _context.FisBankRecords
                .IgnoreQueryFilters()            // also check soft-deleted records
                .AnyAsync(r => fileIds.Contains(r.FisBankFileId)
                            && r.TransferStatus == "SUCCESS");
            return !hasSuccessful;
        }
    }
}