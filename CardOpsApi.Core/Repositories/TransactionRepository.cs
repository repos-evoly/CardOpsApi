using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardOpsApi.Core.Abstractions;
using CardOpsApi.Data.Context;
using CardOpsApi.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using CardOpsApi.Core.Dtos;

namespace CardOpsApi.Data.Repositories
{
    public class TransactionRepository : ITransactionRepository
    {
        private readonly CardOpsApiDbContext _context;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly IHostEnvironment _env;

        public TransactionRepository(CardOpsApiDbContext context, HttpClient httpClient, IConfiguration config, IHostEnvironment env)
        {
            _context = context;
            _httpClient = httpClient;
            _config = config;
            _env = env;
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

            return await query.OrderByDescending(t => t.CreatedAt)
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

        private string GetCoreBaseUrl()
        {
            var prod = _config["CoreApi:BaseUrlProd"] ?? "http://10.1.1.205:7070";
            var nonProd = _config["CoreApi:BaseUrlNonProd"] ?? "http://10.3.3.11:7070";
            return _env.IsProduction() ? prod : nonProd;
        }

        public async Task<string> ResolveExternalAccountNameAsync(string fullAccount)
        {
            if (string.IsNullOrWhiteSpace(fullAccount) || fullAccount.Length != 13)
                return "Not available";
            string cid = fullAccount.Substring(4, 6);

            var baseUrl = GetCoreBaseUrl();
            var payload = new
            {
                Header = new
                {
                    system = "MOBILE",
                    referenceId = Guid.NewGuid().ToString("N").Substring(0, 16).ToUpper(),
                    userName = "TEDMOB",
                    customerNumber = cid,
                    requestTime = DateTime.UtcNow.ToString("o"),
                    language = "AR"
                },
                Details = new Dictionary<string, string>
                {
                    ["@CID"] = cid,
                    ["@GETAVB"] = "Y"
                }
            };

            try
            {
                var resp = await _httpClient.PostAsJsonAsync($"{baseUrl}/api/mobile/accounts", payload);
                if (!resp.IsSuccessStatusCode) return "Not available";
                var ext = await resp.Content.ReadFromJsonAsync<ExternalAccountsResponseDto>();
                if (ext?.Details?.Accounts == null) return "Not available";
                var match = ext.Details.Accounts
                    .Select(acc => new
                    {
                        Key = (acc.YBCD01AB?.Trim() ?? "") + (acc.YBCD01AN?.Trim() ?? "") + (acc.YBCD01AS?.Trim() ?? ""),
                        acc
                    })
                    .FirstOrDefault(x => x.Key == fullAccount);
                return match?.acc.YBCD01CUNA?.Trim() ?? "Not available";
            }
            catch { return "Not available"; }
        }

        private async Task<(bool ok, string message, string requestJson, string responseJson, string referenceId)> PostTransferAsync(TransactionCreateDto dto, ILogger logger)
        {
            string currencyCode = dto.CurrencyId switch
            {
                1 => "LYD",
                2 => "USD",
                3 => "EUR",
                _ => "LYD"
            };

            const int DEC = 3;
            long amountUnits = (long)Math.Round(dto.Amount * (decimal)Math.Pow(10, DEC), MidpointRounding.AwayFromZero);
            string formattedAmt = amountUnits.ToString("D15");

            var refId = Guid.NewGuid().ToString("N").Substring(0, 16).ToUpper();
            var requestObj = new
            {
                Header = new
                {
                    system = "MOBILE",
                    referenceId = refId,
                    userName = "TEDMOB",
                    customerNumber = dto.ToAccount,
                    requestTime = DateTime.UtcNow.ToString("o"),
                    language = "AR"
                },
                Details = new Dictionary<string, string>
                {
                    ["@TRFCCY"] = currencyCode,
                    ["@SRCACC"] = dto.FromAccount,
                    ["@DSTACC"] = dto.ToAccount,
                    ["@DSTACC2"] = "",
                    ["@TRFAMT"] = formattedAmt,
                    ["@APLYTRN2"] = "N",
                    ["@TRFAMT2"] = new string('0', 15),
                    ["@NR2"] = dto.Narrative
                }
            };

            string payloadJson = JsonSerializer.Serialize(requestObj);
            try
            {
                var baseUrl = GetCoreBaseUrl();
                var response = await _httpClient.PostAsJsonAsync($"{baseUrl}/api/mobile/postTransfer", requestObj);
                var raw = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                    return (false, $"External API call failed with status code {response.StatusCode}", payloadJson, raw, refId);

                var respObj = JsonSerializer.Deserialize<ExternalRefundResponseDto>(raw, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (respObj?.Header == null)
                    return (false, "Malformed response from external API.", payloadJson, raw, refId);

                bool ok = string.Equals(respObj.Header.ReturnCode, "Success", StringComparison.OrdinalIgnoreCase);
                return (ok, respObj.Header.ReturnMessage, payloadJson, raw, refId);
            }
            catch (HttpRequestException ex)
            {
                logger.LogError(ex, "[PostTransferAsync] HTTP error");
                return (false, $"HTTP Request Exception: {ex.Message}", payloadJson, string.Empty, refId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[PostTransferAsync] Unexpected error");
                return (false, $"Error: {ex.Message}", payloadJson, string.Empty, refId);
            }
        }

        public async Task<(bool success, string message, Transactions? saved)> CreateWithCoreAsync(TransactionCreateDto dto, ILogger logger)
        {
            if (!string.IsNullOrWhiteSpace(dto.Status) && dto.Status.ToLower().Contains("refund"))
            {
                var result = await PostTransferAsync(dto, logger);
                if (!result.ok)
                    return (false, result.message, null);

                var entity = new Transactions
                {
                    FromAccount = dto.FromAccount,
                    ToAccount = dto.ToAccount,
                    Amount = dto.Amount,
                    Narrative = dto.Narrative,
                    Date = dto.Date,
                    Status = dto.Status,
                    Type = dto.Type,
                    DefinitionId = dto.DefinitionId,
                    CurrencyId = dto.CurrencyId,
                    ReasonId = dto.ReasonId,
                    ReferenceId = result.referenceId,
                    CoreRequest = result.requestJson,
                    CoreResponse = result.responseJson
                };
                await CreateAsync(entity);
                return (true, result.message, entity);
            }
            else
            {
                var entity = new Transactions
                {
                    FromAccount = dto.FromAccount,
                    ToAccount = dto.ToAccount,
                    Amount = dto.Amount,
                    Narrative = dto.Narrative,
                    Date = dto.Date,
                    Status = dto.Status,
                    Type = dto.Type,
                    DefinitionId = dto.DefinitionId,
                    CurrencyId = dto.CurrencyId,
                    ReasonId = dto.ReasonId
                };
                await CreateAsync(entity);
                return (true, "Created", entity);
            }
        }

        public async Task<(bool success, string message, Transactions? saved)> ReverseRefundAsync(int originalTransactionId, ILogger logger)
        {
            var original = await _context.Transactions.FindAsync(originalTransactionId);
            if (original == null)
                return (false, "Original transaction not found.", null);

            string cleanNarr = System.Text.RegularExpressions.Regex.Replace($"Reversal of transaction {original.Id}", @"[^A-Za-z0-9 ]", "");
            var dto = new TransactionCreateDto
            {
                FromAccount = original.ToAccount,
                ToAccount = original.FromAccount,
                Amount = original.Amount,
                Narrative = cleanNarr,
                Status = original.Status,
                Type = original.Type,
                DefinitionId = original.DefinitionId,
                CurrencyId = original.CurrencyId,
                ReasonId = original.ReasonId,
                Date = DateTimeOffset.Now
            };

            var result = await PostTransferAsync(dto, logger);
            if (!result.ok)
                return (false, result.message, null);

            var entity = new Transactions
            {
                FromAccount = dto.FromAccount,
                ToAccount = dto.ToAccount,
                Amount = dto.Amount,
                Narrative = dto.Narrative,
                Date = dto.Date,
                Status = dto.Status,
                Type = dto.Type,
                DefinitionId = dto.DefinitionId,
                CurrencyId = dto.CurrencyId,
                ReasonId = dto.ReasonId,
                ReverseRefId = result.referenceId,
                CoreRequest = result.requestJson,
                CoreResponse = result.responseJson
            };
            await CreateAsync(entity);
            return (true, result.message, entity);
        }

        public async Task<List<ExternalTransactionDto>> GetExternalTransactionsAsync(string account, DateTime fromDate, DateTime toDate)
        {
            var baseUrl = GetCoreBaseUrl();
            var referenceId = Guid.NewGuid().ToString("N").Substring(0, 16).ToUpper();
            var payload = new
            {
                Header = new
                {
                    system = "MOBILE",
                    referenceId = referenceId,
                    userName = "TEDMOB",
                    customerNumber = account,
                    requestTime = DateTime.UtcNow.ToString("o"),
                    language = "AR"
                },
                Details = new Dictionary<string, string>
                {
                    {"@TID", referenceId},
                    {"@ACC", account},
                    {"@BYDTE", "Y"},
                    {"@FDATE", fromDate.ToString("yyyyMMdd")},
                    {"@TDATE", toDate.ToString("yyyyMMdd")},
                    {"@BYNBR", "N"},
                    {"@NBR", "2"}
                }
            };

            var resp = await _httpClient.PostAsJsonAsync($"{baseUrl}/api/mobile/transactions", payload);
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"External API error ({resp.StatusCode}): {body}");

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("Details", out var details))
                return new List<ExternalTransactionDto>();
            if (!details.TryGetProperty("Transactions", out var txns) || txns.ValueKind != JsonValueKind.Array)
                return new List<ExternalTransactionDto>();

            return txns.EnumerateArray().Select(el => new ExternalTransactionDto
            {
                PostingDate = el.GetProperty("YBCD04POD").GetString()?.Trim(),
                Narratives = new List<string>(new[]
                {
                    el.GetProperty("YBCD04NAR1").GetString()?.Trim(),
                    el.GetProperty("YBCD04NAR2").GetString()?.Trim()
                }.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!)) ,
                Amount = el.GetProperty("YBCD04AMA").GetDecimal(),
                DrCr = el.GetProperty("YBCD04DRCR").GetString()
            }).ToList();
        }

        public async Task<bool> CheckAccountAvailabilityAsync(string account)
        {
            var name = await ResolveExternalAccountNameAsync(account);
            return name != "Not available";
        }

        public class ExternalRefundResponseHeaderDto
        {
            public string System { get; set; } = string.Empty;
            public string ReferenceId { get; set; } = string.Empty;
            public string ReturnCode { get; set; } = string.Empty;
            public string ReturnMessageCode { get; set; } = string.Empty;
            public string ReturnMessage { get; set; } = string.Empty;
            public string CurCode { get; set; } = string.Empty;
            public string CurDescrip { get; set; } = string.Empty;
        }
        public class ExternalRefundResponseDto
        {
            public ExternalRefundResponseHeaderDto Header { get; set; } = new ExternalRefundResponseHeaderDto();
        }
        public class ExternalAccountDto
        {
            public string? YBCD01AB { get; set; }
            public string? YBCD01AN { get; set; }
            public string? YBCD01AS { get; set; }
            public string? YBCD01CUNA { get; set; }
        }
        public class ExternalAccountsResponseDetailsDto
        {
            public List<ExternalAccountDto> Accounts { get; set; } = new List<ExternalAccountDto>();
        }
        public class ExternalAccountsResponseDto
        {
            public ExternalRefundResponseHeaderDto Header { get; set; } = new ExternalRefundResponseHeaderDto();
            public ExternalAccountsResponseDetailsDto Details { get; set; } = new ExternalAccountsResponseDetailsDto();
        }
    }
}
