using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardOpsApi.Core.Abstractions;
using CardOpsApi.Data.Context;
using CardOpsApi.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CardOpsApi.Core.Repositories
{
    public class ReasonRepository : IReasonRepository
    {
        private readonly CardOpsApiDbContext _context;
        public ReasonRepository(CardOpsApiDbContext context)
        {
            _context = context;
        }

        public async Task CreateAsync(Reason reason)
        {
            _context.Reasons.Add(reason);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var reason = await _context.Reasons.FindAsync(id);
            if (reason != null)
            {
                _context.Reasons.Remove(reason);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IList<Reason>> GetAllAsync(string? searchTerm, string? searchBy, int page, int limit)
        {
            IQueryable<Reason> query = _context.Reasons.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch (searchBy?.ToLower())
                {
                    case "namelt":
                        query = query.Where(r => r.NameLT.Contains(searchTerm));
                        break;
                    case "namear":
                        query = query.Where(r => r.NameAR.Contains(searchTerm));
                        break;
                    case "description":
                        query = query.Where(r => r.Description != null && r.Description.Contains(searchTerm));
                        break;
                    default:
                        query = query.Where(r => r.NameLT.Contains(searchTerm) || r.NameAR.Contains(searchTerm) ||
                                                   (r.Description != null && r.Description.Contains(searchTerm)));
                        break;
                }
            }

            return await query.OrderBy(r => r.Id)
                              .Skip((page - 1) * limit)
                              .Take(limit)
                              .AsNoTracking()
                              .ToListAsync();
        }

        public async Task<int> GetCountAsync(string? searchTerm, string? searchBy)
        {
            IQueryable<Reason> query = _context.Reasons.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                switch (searchBy?.ToLower())
                {
                    case "namelt":
                        query = query.Where(r => r.NameLT.Contains(searchTerm));
                        break;
                    case "namear":
                        query = query.Where(r => r.NameAR.Contains(searchTerm));
                        break;
                    case "description":
                        query = query.Where(r => r.Description != null && r.Description.Contains(searchTerm));
                        break;
                    default:
                        query = query.Where(r => r.NameLT.Contains(searchTerm) ||
                                                 r.NameAR.Contains(searchTerm) ||
                                                 (r.Description != null && r.Description.Contains(searchTerm)));
                        break;
                }
            }

            return await query.AsNoTracking().CountAsync();
        }

        public async Task<Reason?> GetByIdAsync(int id)
        {
            return await _context.Reasons.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task UpdateAsync(Reason reason)
        {
            _context.Reasons.Update(reason);
            await _context.SaveChangesAsync();
        }
    }
}
