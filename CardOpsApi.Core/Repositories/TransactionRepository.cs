using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardOpsApi.Core.Abstractions;
using CardOpsApi.Data.Context;
using CardOpsApi.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CardOpsApi.Data.Repositories
{
    public class TransactionRepository : ITransactionRepository
    {
        private readonly CardOpsApiDbContext _context;

        public TransactionRepository(CardOpsApiDbContext context)
        {
            _context = context;
        }

        public async Task CreateAsync(Transactions transaction)
        {
            await _context.Transactions.AddAsync(transaction);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var transaction = await _context.Transactions.FindAsync(id);
            if (transaction != null)
            {
                _context.Transactions.Remove(transaction);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IList<Transactions>> GetAllAsync(string? searchTerm, string? searchBy, string? type, int page, int limit)
        {
            IQueryable<Transactions> query = _context.Transactions
                .Include(t => t.Currency)
                .Include(t => t.Reason)
                .Include(t => t.Definition);

            if (!string.IsNullOrWhiteSpace(type))
                query = query.Where(t => t.Type.ToLower() == type.ToLower());

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch (searchBy?.ToLower())
                {
                    case "fromaccount":
                        query = query.Where(t => t.FromAccount.Contains(searchTerm));
                        break;
                    case "narrative":
                        query = query.Where(t => t.Narrative.Contains(searchTerm));
                        break;
                    case "status":
                        query = query.Where(t => t.Status != null && t.Status.Contains(searchTerm));
                        break;
                    case "currency":
                        query = query.Where(t => t.Currency.Code.Contains(searchTerm));
                        break;
                    default:
                        query = query.Where(t => t.FromAccount.Contains(searchTerm)
                                               || t.Narrative.Contains(searchTerm)
                                               || (t.Status != null && t.Status.Contains(searchTerm))
                                               || t.Currency.Code.Contains(searchTerm));
                        break;
                }
            }

            return await query.OrderByDescending(t => t.Date)
                              .Skip((page - 1) * limit)
                              .Take(limit)
                              .AsNoTracking()
                              .ToListAsync();
        }
        public async Task<int> GetCountAsync(string? searchTerm, string? searchBy, string? type)
        {
            IQueryable<Transactions> query = _context.Transactions
                .Include(t => t.Currency)
                .Include(t => t.Reason)
                .Include(t => t.Definition);

            if (!string.IsNullOrWhiteSpace(type))
                query = query.Where(t => t.Type.ToLower() == type.ToLower());

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch (searchBy?.ToLower())
                {
                    case "fromaccount":
                        query = query.Where(t => t.FromAccount.Contains(searchTerm));
                        break;
                    case "narrative":
                        query = query.Where(t => t.Narrative.Contains(searchTerm));
                        break;
                    case "status":
                        query = query.Where(t => t.Status != null && t.Status.Contains(searchTerm));
                        break;
                    case "currency":
                        query = query.Where(t => t.Currency.Code.Contains(searchTerm));
                        break;
                    default:
                        query = query.Where(t => t.FromAccount.Contains(searchTerm)
                                               || t.Narrative.Contains(searchTerm)
                                               || (t.Status != null && t.Status.Contains(searchTerm))
                                               || t.Currency.Code.Contains(searchTerm));
                        break;
                }
            }

            return await query.AsNoTracking().CountAsync();
        }

        public async Task<Transactions?> GetByIdAsync(int id)
        {
            return await _context.Transactions
                                 .Include(t => t.Currency)
                                 .Include(t => t.Reason)
                                 .AsNoTracking()
                                 .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task UpdateAsync(Transactions transaction)
        {
            _context.Transactions.Update(transaction);
            await _context.SaveChangesAsync();
        }

        public async Task<(int atmCount, int posCount, decimal totalPosAmount, decimal totalAtmAmount)> GetStatsAsync()
        {
            var atmCount = await _context.Transactions.CountAsync(t => t.Type == "ATM");
            var posCount = await _context.Transactions.CountAsync(t => t.Type == "POS");
            var totalPosAmount = await _context.Transactions.Where(t => t.Type == "POS").SumAsync(t => t.Amount);
            var totalAtmAmount = await _context.Transactions.Where(t => t.Type == "ATM").SumAsync(t => t.Amount);

            return (atmCount, posCount, totalPosAmount, totalAtmAmount);
        }

        public async Task<List<(string AtmAccount, int RefundCount)>> GetTopRefundAtmsAsync()
        {
            return await _context.Transactions
                .Where(t => t.Type == "ATM" && t.Status == "REFUND")
                .GroupBy(t => t.FromAccount)
                .Select(g => new ValueTuple<string, int>(g.Key, g.Count()))
                .OrderByDescending(g => g.Item2)
                .Take(10)
                .ToListAsync();
        }
    }
}
