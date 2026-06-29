using AutoMapper;
using CardOpsApi.Core.Abstractions;
using CardOpsApi.Core.Dtos;
using CardOpsApi.Data.Context;
using CardOpsApi.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CardOpsApi.Core.Repositories
{
    public class ManualReverseRepository : IManualReverseRepository
    {
        private readonly CardOpsApiDbContext _context;
        private readonly IMapper             _mapper;

        public ManualReverseRepository(CardOpsApiDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper  = mapper;
        }

        public async Task<ManualReverse> CreateAsync(ManualReverse manualreverse)
        {
            await _context.ManualReverse.AddAsync(manualreverse);
            await _context.SaveChangesAsync();
            return manualreverse;
        }

        public async Task<IList<ManualReverseDto>> GetAllAsync(
            DateTime? fromDate  = null,
            DateTime? toDate    = null,
            string?   keyword   = null,
            string?   accountNumber = null,
            int       page      = 1,
            int       limit     = 100000)
        {
            var query = BuildQuery(fromDate, toDate, keyword, accountNumber);

            var transfers = await query
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * limit)
                .Take(limit)
                .AsNoTracking()
                .ToListAsync();

            return transfers
                .Select(t => _mapper.Map<ManualReverseDto>(t))
                .ToList();
        }

        public async Task<int> GetCountAsync(
            DateTime? fromDate  = null,
            DateTime? toDate    = null,
            string?   keyword   = null,
            string?   accountNumber = null)
        {
            return await BuildQuery(fromDate, toDate, keyword, accountNumber).CountAsync();
        }

        private IQueryable<ManualReverse> BuildQuery(
            DateTime? fromDate,
            DateTime? toDate,
            string?   keyword,
            string?   accountNumber)
        {
            var query = _context.ManualReverse.AsQueryable();

            if (fromDate.HasValue)
                query = query.Where(t => t.ManualReverseDate >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(t => t.ManualReverseDate < toDate.Value.AddDays(1));

            if (!string.IsNullOrWhiteSpace(accountNumber))
                query = query.Where(t => t.DestinationAccount == accountNumber);  // ADD THIS BLOCK

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim();
                query = query.Where(t =>
                    t.DestinationAccount.Contains(kw)                                  ||
                    t.TransferStatus.Contains(kw)                                      ||
                    (t.TransferReference != null && t.TransferReference.Contains(kw))  ||
                    (t.ResponseMessage   != null && t.ResponseMessage.Contains(kw)));
            }

            return query;
        }
        public async Task<ManualReverse?> GetByIdAsync(int id)
        {
            return await _context.ManualReverse
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task UpdateStatusAsync(int id, string status, string? reference, string? message)
        {
            var transfer = await _context.ManualReverse.FindAsync(id);
            if (transfer == null) return;

            transfer.TransferStatus    = status;
            transfer.TransferReference = reference;
            transfer.ResponseMessage   = message;
            if (status == "SUCCESS")
            {
                transfer.ManualReverseExecuteDate = DateTime.UtcNow;
                
            }

            await _context.SaveChangesAsync();
        }
    }
}