using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardOpsApi.Core.Abstractions;
using CardOpsApi.Data.Context;
using CardOpsApi.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CardOpsApi.Data.Repositories
{
    public class DefinitionRepository : IDefinitionRepository
    {
        private readonly CardOpsApiDbContext _context;

        public DefinitionRepository(CardOpsApiDbContext context)
        {
            _context = context;
        }

        public async Task CreateAsync(Definition definition)
        {
            await _context.Definitions.AddAsync(definition);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var definition = await _context.Definitions.FindAsync(id);
            if (definition != null)
            {
                _context.Definitions.Remove(definition);
                await _context.SaveChangesAsync();
            }
        }

        // Supports optional search, filtering by type, and pagination.
        public async Task<IList<Definition>> GetAllAsync(string? searchTerm, string? searchBy, string? type, int page, int limit)
        {
            // Include Currency so that we can filter on Currency.Code and map to DTOs.
            IQueryable<Definition> query = _context.Definitions.Include(d => d.Currency);

            // Filter by type if provided (case-insensitive)
            if (!string.IsNullOrWhiteSpace(type))
            {
                var loweredType = type.Trim().ToLower();
                query = query.Where(d => d.Type.ToLower() == loweredType);
            }

            // Apply search conditions
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.Trim();
                if (!string.IsNullOrWhiteSpace(searchBy))
                {
                    switch (searchBy.ToLower())
                    {
                        case "accountnumber":
                            query = query.Where(d => d.AccountNumber.Contains(searchTerm));
                            break;
                        case "name":
                            query = query.Where(d => d.Name.Contains(searchTerm));
                            break;
                        case "curr":
                            // Search based on the related Currency's Code.
                            query = query.Where(d => d.Currency.Code.Contains(searchTerm));
                            break;
                        case "type":
                            query = query.Where(d => d.Type.Contains(searchTerm));
                            break;
                        default:
                            query = query.Where(d => d.AccountNumber.Contains(searchTerm) ||
                                                     d.Name.Contains(searchTerm) ||
                                                     d.Currency.Code.Contains(searchTerm) ||
                                                     d.Type.Contains(searchTerm));
                            break;
                    }
                }
                else
                {
                    query = query.Where(d => d.AccountNumber.Contains(searchTerm) ||
                                             d.Name.Contains(searchTerm) ||
                                             d.Currency.Code.Contains(searchTerm) ||
                                             d.Type.Contains(searchTerm));
                }
            }

            query = query.OrderBy(d => d.Id)
                         .Skip((page - 1) * limit)
                         .Take(limit);

            return await query.AsNoTracking().ToListAsync();
        }

        public async Task<int> GetCountAsync(string? searchTerm, string? searchBy, string? type)
        {
            IQueryable<Definition> query = _context.Definitions.Include(d => d.Currency);

            if (!string.IsNullOrWhiteSpace(type))
            {
                var loweredType = type.Trim().ToLower();
                query = query.Where(d => d.Type.ToLower() == loweredType);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.Trim();
                if (!string.IsNullOrWhiteSpace(searchBy))
                {
                    switch (searchBy.ToLower())
                    {
                        case "accountnumber":
                            query = query.Where(d => d.AccountNumber.Contains(searchTerm));
                            break;
                        case "name":
                            query = query.Where(d => d.Name.Contains(searchTerm));
                            break;
                        case "curr":
                            query = query.Where(d => d.Currency.Code.Contains(searchTerm));
                            break;
                        case "type":
                            query = query.Where(d => d.Type.Contains(searchTerm));
                            break;
                        default:
                            query = query.Where(d => d.AccountNumber.Contains(searchTerm) ||
                                                     d.Name.Contains(searchTerm) ||
                                                     d.Currency.Code.Contains(searchTerm) ||
                                                     d.Type.Contains(searchTerm));
                            break;
                    }
                }
                else
                {
                    query = query.Where(d => d.AccountNumber.Contains(searchTerm) ||
                                             d.Name.Contains(searchTerm) ||
                                             d.Currency.Code.Contains(searchTerm) ||
                                             d.Type.Contains(searchTerm));
                }
            }

            return await query.AsNoTracking().CountAsync();
        }

        public async Task<Definition?> GetByIdAsync(int id)
        {
            return await _context.Definitions
                .Include(d => d.Currency)
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == id);
        }

        public async Task UpdateAsync(Definition definition)
        {
            _context.Definitions.Update(definition);
            await _context.SaveChangesAsync();
        }
    }
}
