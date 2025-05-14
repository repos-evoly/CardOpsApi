using System.Linq;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Security.Claims;
using AutoMapper;
using CardOpsApi.Core.Abstractions;
using CardOpsApi.Core.Dtos;
using CardOpsApi.Data.Models;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CardOpsApi.Abstractions;
using CardOpsApi.Data.Abstractions;
using System.Text.Json;
using System.Net.Http.Headers;

namespace CardOpsApi.Endpoints
{
    public class TransactionEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            // Require a specific policy for transactions – adjust as needed.
            var transactions = app.MapGroup("/api/transactions").RequireAuthorization("requireAuthUser");

            transactions.MapGet("/", GetTransactions)
                        .WithName("GetTransactions")
                        .Produces<List<TransactionDto>>(200);

            transactions.MapGet("/{id:int}", GetTransactionById)
                        .WithName("GetTransactionById")
                        .Produces<TransactionDto>(200)
                        .Produces(404);

            transactions.MapPost("/", CreateTransaction)
                        .WithName("CreateTransaction")
                        .Accepts<TransactionCreateDto>("application/json")
                        .Produces<TransactionDto>(201)
                        .Produces(400);

            transactions.MapPut("/{id:int}", UpdateTransaction)
                        .WithName("UpdateTransaction")
                        .Accepts<TransactionUpdateDto>("application/json")
                        .Produces<TransactionDto>(200)
                        .Produces(400)
                        .Produces(404);

            transactions.MapDelete("/{id:int}", DeleteTransaction)
                        .WithName("DeleteTransaction")
                        .Produces(200)
                        .Produces(404);

            // Reporting endpoints:
            // Summary for transactions: number of ATM and POS transactions and total amounts.
            transactions.MapGet("/summary", GetTransactionSummary)
                        .WithName("GetTransactionSummary")
                        .Produces<TransactionSummaryDto>(200);

            // Top 5 ATMs with most refunds.
            transactions.MapGet("/top-atm-refunds", GetTopAtmRefunds)
                        .WithName("GetTopAtmRefunds")
                        .Produces<List<TopAtmRefundDto>>(200);

            // Top 10 reasons with most transactions.
            transactions.MapGet("/top-reasons", GetTopReasons)
                        .WithName("GetTopReasons")
                        .Produces<List<TopReasonDto>>(200);

            transactions.MapGet("/check-account", CheckAccountAvailability)
                        .WithName("CheckAccountAvailability")
                        .Produces(200)
                        .Produces(400);
            transactions.MapGet("/external", GetExternalTransactions)
                        .WithName("GetExternalTransactions")
                        .Produces<List<ExternalTransactionDto>>(200)
                        .Produces(400);

        }

        // GET /api/transactions?searchTerm=&searchBy=&type=&page=&limit=
        public static async Task<IResult> GetTransactions(
           [FromServices] ITransactionRepository transactionRepository,
           [FromServices] IMapper mapper,
           [FromQuery] string? searchTerm,
           [FromQuery] string? searchBy,
           [FromQuery] string? type,
           [FromQuery] int page = 1,
           [FromQuery] int limit = 100000)
        {
            // Get the current page data.
            var transactions = await transactionRepository.GetAllAsync(searchTerm, searchBy, type, page, limit);

            // Get the total count for the same filters.
            int totalCount = await transactionRepository.GetCountAsync(searchTerm, searchBy, type);
            int totalPages = (int)System.Math.Ceiling((double)totalCount / limit);

            var transactionDtos = mapper.Map<List<TransactionDto>>(transactions);
            return Results.Ok(new { Data = transactionDtos, TotalPages = totalPages });
        }

        // GET /api/transactions/{id}
        public static async Task<IResult> GetTransactionById(
            int id,
            [FromServices] ITransactionRepository transactionRepository,
            [FromServices] IMapper mapper)
        {
            var transaction = await transactionRepository.GetByIdAsync(id);
            if (transaction == null)
            {
                return Results.NotFound("Transaction not found.");
            }
            var dto = mapper.Map<TransactionDto>(transaction);
            return Results.Ok(dto);
        }

        // POST /api/transactions
        public static async Task<IResult> CreateTransaction(
            [FromBody] TransactionCreateDto createDto,
            [FromServices] ITransactionRepository transactionRepository,
            [FromServices] IMapper mapper,
            [FromServices] IValidator<TransactionCreateDto> validator)
        {
            ValidationResult validationResult = await validator.ValidateAsync(createDto);
            if (!validationResult.IsValid)
            {
                return Results.BadRequest(validationResult.Errors.Select(e => e.ErrorMessage));
            }

            // If the Status indicates a refund, call the external API first.
            if (!string.IsNullOrWhiteSpace(createDto.Status) &&
                createDto.Status.ToLower().Contains("refund"))
            {
                var refundResult = await CallExternalRefundAsync(createDto);
                if (!refundResult.isSuccess)
                {
                    // Return the error message from the external API.
                    return Results.BadRequest(refundResult.message);
                }
            }

            // Add the transaction to our system.
            var transaction = mapper.Map<Transactions>(createDto);
            await transactionRepository.CreateAsync(transaction);
            var dto = mapper.Map<TransactionDto>(transaction);

            // Construct a success message.
            string successMessage = $"Refund was successful from {createDto.FromAccount} to {createDto.ToAccount} with amount {createDto.Amount}.";

            return Results.Created($"/api/transactions/{dto.Id}", new { Message = successMessage, Transaction = dto });
        }

        // PUT /api/transactions/{id}
        public static async Task<IResult> UpdateTransaction(
      int id,
      [FromBody] TransactionUpdateDto updateDto,
      [FromServices] ITransactionRepository transactionRepository,
      [FromServices] IMapper mapper,
      [FromServices] IValidator<TransactionUpdateDto> validator,
      ILogger<TransactionEndpoints> logger)
        {
            // Retrieve the existing transaction.
            var existingTransaction = await transactionRepository.GetByIdAsync(id);
            if (existingTransaction == null)
            {
                return Results.NotFound("Transaction not found.");
            }

            // Log the incoming DTO ReasonId.
            logger.LogInformation("[UpdateTransaction] Incoming ReasonId from DTO: {ReasonId}", updateDto.ReasonId);

            // Validate the DTO.
            ValidationResult validationResult = await validator.ValidateAsync(updateDto);
            if (!validationResult.IsValid)
            {
                return Results.BadRequest(validationResult.Errors.Select(e => e.ErrorMessage));
            }

            // Map all fields from the DTO into the existing entity.
            mapper.Map(updateDto, existingTransaction);

            // Explicitly reassign ReasonId from the DTO.
            existingTransaction.ReasonId = updateDto.ReasonId;
            // Clear the navigation property to ensure EF Core uses the new ReasonId.
            existingTransaction.Reason = null;

            // Log the final ReasonId that will be saved.
            logger.LogInformation("[UpdateTransaction] Final ReasonId set on entity: {ReasonId}", existingTransaction.ReasonId);

            // Update the transaction in the repository.
            await transactionRepository.UpdateAsync(existingTransaction);
            var dto = mapper.Map<TransactionDto>(existingTransaction);

            logger.LogInformation("[UpdateTransaction] Updated transaction {TransactionId} with ReasonId {ReasonId}", id, existingTransaction.ReasonId);
            return Results.Ok(dto);
        }




        // DELETE /api/transactions/{id}
        public static async Task<IResult> DeleteTransaction(
            int id,
            [FromServices] ITransactionRepository transactionRepository)
        {
            var existingTransaction = await transactionRepository.GetByIdAsync(id);
            if (existingTransaction == null)
            {
                return Results.NotFound("Transaction not found.");
            }
            await transactionRepository.DeleteAsync(id);
            return Results.Ok("Transaction deleted successfully.");
        }

        // GET /api/transactions/summary?year={year}
        public static async Task<IResult> GetTransactionSummary(
            [FromServices] ITransactionRepository transactionRepository,
            [FromQuery] int? year)
        {
            // Retrieve all transactions.
            var transactions = await transactionRepository.GetAllAsync(null, null, null, 1, int.MaxValue);
            if (year.HasValue)
            {
                transactions = transactions.Where(t => t.Date.Year == year.Value).ToList();
            }
            var atmCount = transactions.Count(t => t.Type.Equals("ATM", System.StringComparison.OrdinalIgnoreCase));
            var posCount = transactions.Count(t => t.Type.Equals("POS", System.StringComparison.OrdinalIgnoreCase));
            var posTotalAmount = transactions.Where(t => t.Type.Equals("POS", System.StringComparison.OrdinalIgnoreCase)).Sum(t => t.Amount);
            var atmTotalAmount = transactions.Where(t => t.Type.Equals("ATM", System.StringComparison.OrdinalIgnoreCase)).Sum(t => t.Amount);
            var summary = new TransactionSummaryDto
            {
                AtmCount = atmCount,
                PosCount = posCount,
                PosTotalAmount = posTotalAmount,
                AtmTotalAmount = atmTotalAmount
            };
            return Results.Ok(summary);
        }

        // GET /api/transactions/top-atm-refunds?year={year}
        // GET /api/transactions/top-atm-refunds?year={year}
        public static async Task<IResult> GetTopAtmRefunds(
            [FromServices] ITransactionRepository transactionRepository,
            [FromServices] ISettingsRepository settingsRepository,
            [FromQuery] int? year)
        {
            // Retrieve all transactions.
            var transactions = await transactionRepository.GetAllAsync(null, null, null, 1, int.MaxValue);
            if (year.HasValue)
            {
                transactions = transactions.Where(t => t.Date.Year == year.Value).ToList();
            }

            // Retrieve settings to determine how many rows to return.
            var settings = await settingsRepository.GetFirstSettingsAsync();
            int atmRefundLimit = settings?.TopAtmRefundLimit ?? 5; // default to 5 if not set

            var atmRefunds = transactions
                .Where(t => t.Definition != null &&
                            t.Definition.Type.Equals("ATM", System.StringComparison.OrdinalIgnoreCase) &&
                            !string.IsNullOrWhiteSpace(t.Status) &&
                            t.Status.ToLower().Contains("refund"))
                .GroupBy(t => t.Definition.AccountNumber)
                .Select(g => new TopAtmRefundDto
                {
                    AtmIdentifier = g.Key,
                    RefundCount = g.Count()
                })
                .OrderByDescending(x => x.RefundCount)
                .Take(atmRefundLimit)
                .ToList();

            return Results.Ok(atmRefunds);
        }

        // GET /api/transactions/top-reasons?year={year}
        public static async Task<IResult> GetTopReasons(
            [FromServices] ITransactionRepository transactionRepository,
            [FromServices] ISettingsRepository settingsRepository,
            [FromQuery] int? year)
        {
            var transactions = await transactionRepository.GetAllAsync(null, null, null, 1, int.MaxValue);
            if (year.HasValue)
            {
                transactions = transactions.Where(t => t.Date.Year == year.Value).ToList();
            }

            // Retrieve settings to determine how many rows to return.
            var settings = await settingsRepository.GetFirstSettingsAsync();
            int topReasonsLimit = settings?.TopReasonLimit ?? 10; // default to 10 if not set

            var topReasons = transactions
                .Where(t => t.Reason != null)
                .GroupBy(t => new { t.Reason.Id, t.Reason.NameAR })
                .Select(g => new TopReasonDto
                {
                    ReasonId = g.Key.Id,
                    ReasonName = g.Key.NameAR,
                    TransactionCount = g.Count()
                })
                .OrderByDescending(r => r.TransactionCount)
                .Take(topReasonsLimit)
                .ToList();

            return Results.Ok(topReasons);
        }

        // Private method to call the external refund API.
        private static async Task<(bool isSuccess, string message)> CallExternalRefundAsync(TransactionCreateDto createDto)
        {
            // Determine the currency code based on CurrencyId.
            string currencyCode = createDto.CurrencyId switch
            {
                1 => "LYD",
                2 => "USD",
                3 => "EUR",
                _ => "LYD"
            };

            // === ALWAYS USE 3 DECIMAL-PLACES ===
            const int DECIMALS = 3;
            decimal scaleFactor = (decimal)Math.Pow(10, DECIMALS);  // 1000
            long amountInUnits = (long)Math.Round(createDto.Amount * scaleFactor, MidpointRounding.AwayFromZero);
            string formattedAmount = amountInUnits.ToString("D15");

            var requestObj = new
            {
                Header = new
                {
                    system = "MOBILE",
                    referenceId = GenerateReferenceId(),
                    userName = "TEDMOB",
                    customerNumber = createDto.ToAccount,
                    requestTime = DateTime.UtcNow.ToString("o"),
                    language = "AR"
                },
                Details = new Dictionary<string, string>
                {
                    ["@TRFCCY"] = currencyCode,
                    ["@SRCACC"] = createDto.FromAccount,
                    ["@DSTACC"] = createDto.ToAccount,
                    ["@DSTACC2"] = "",
                    ["@TRFAMT"] = formattedAmount,
                    ["@APLYTRN2"] = "N",
                    ["@TRFAMT2"] = new string('0', 15),
                    ["@NR2"] = createDto.Narrative
                }
            };

            try
            {
                using var client = new HttpClient();
                string url = "http://10.3.3.11:7070/api/mobile/postTransfer";
                var response = await client.PostAsJsonAsync(url, requestObj);

                if (!response.IsSuccessStatusCode)
                {
                    return (false, $"External API call failed with status code {response.StatusCode}");
                }

                var respObj = await response.Content.ReadFromJsonAsync<ExternalRefundResponseDto>();
                if (respObj?.Header == null)
                {
                    return (false, "Malformed response from external API.");
                }

                bool success = respObj.Header.ReturnCode
                    .Equals("Success", StringComparison.OrdinalIgnoreCase);

                return (success, respObj.Header.ReturnMessage);
            }
            catch (HttpRequestException ex)
            {
                return (false, $"HTTP Request Exception: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}");
            }
        }
        public static async Task<IResult> CheckAccountAvailability(
            [FromQuery] string account,
            [FromServices] ISettingsRepository settingsRepository)
        {
            // Log the input account.
            Console.WriteLine($"[CheckAccountAvailability] Input account: {account}");

            // Validate input length.
            if (string.IsNullOrWhiteSpace(account) || account.Length != 13)
            {
                return Results.BadRequest("Account number must be exactly 13 digits.");
            }

            // Extract digits 5 to 10 (zero-based index 4 with length 6).
            string extractedSixDigits = account.Substring(4, 6);
            Console.WriteLine($"[CheckAccountAvailability] Extracted six-digit value: {extractedSixDigits}");

            // Build external API request payload.
            var requestObj = new
            {
                Header = new
                {
                    system = "MOBILE",
                    referenceId = GenerateReferenceId(),
                    userName = "TEDMOB",
                    customerNumber = extractedSixDigits, // the 6-digit value extracted
                    requestTime = System.DateTime.UtcNow.ToString("o"),
                    language = "AR"
                },
                Details = new Dictionary<string, string>
        {
            { "@CID", extractedSixDigits },
            { "@GETAVB", "Y" }
        }
            };

            try
            {
                using var client = new HttpClient();
                string url = "http://10.1.1.205:7070/api/mobile/accounts";
                var response = await client.PostAsJsonAsync(url, requestObj);
                if (!response.IsSuccessStatusCode)
                {
                    return Results.BadRequest($"External API call failed with status code {response.StatusCode}");
                }

                // Deserialize the external API response.
                var externalResponse = await response.Content.ReadFromJsonAsync<ExternalAccountsResponseDto>();
                if (externalResponse?.Details?.Accounts == null)
                {
                    return Results.BadRequest("Malformed response from external API.");
                }

                // Log each concatenated account.
                foreach (var acc in externalResponse.Details.Accounts)
                {
                    var concatenated = (acc.YBCD01AB?.Trim() ?? "") +
                                       (acc.YBCD01AN?.Trim() ?? "") +
                                       (acc.YBCD01AS?.Trim() ?? "");
                    Console.WriteLine($"[CheckAccountAvailability] External concatenated account: {concatenated}");
                }

                // Build the concatenated account string for each account in the external response.
                var concatenatedAccounts = externalResponse.Details.Accounts
                    .Select(acc => new
                    {
                        AccountString = (acc.YBCD01AB?.Trim() ?? "") +
                                        (acc.YBCD01AN?.Trim() ?? "") +
                                        (acc.YBCD01AS?.Trim() ?? ""),
                        Account = acc // keep the full account object for later
                    });

                // Check if any concatenated account matches the provided 13-digit account.
                var matchingAccountInfo = concatenatedAccounts
                    .FirstOrDefault(x => x.AccountString.Equals(account, System.StringComparison.OrdinalIgnoreCase));

                if (matchingAccountInfo != null)
                {
                    var accountConcatenated = matchingAccountInfo.Account.YBCD01AB?.Trim() +
                                          matchingAccountInfo.Account.YBCD01AN?.Trim() +
                                          matchingAccountInfo.Account.YBCD01AS?.Trim();
                    Console.WriteLine("[CheckAccountAvailability] Match found.");
                    return Results.Ok(new
                    {
                        message = "Account was found",
                        code = "accavv",
                        account = accountConcatenated,
                    });
                }
                else
                {
                    Console.WriteLine("[CheckAccountAvailability] No matching account found.");
                    return Results.Ok(new { message = "Account not found", code = "accnff" });
                }
            }
            catch (HttpRequestException ex)
            {
                return Results.BadRequest($"HTTP Request Exception: {ex.Message}");
            }
            catch (System.Exception ex)
            {
                return Results.BadRequest($"Error: {ex.Message}");
            }

            // Local helper method to generate a 16-character uppercase reference ID.
            string GenerateReferenceId()
            {
                return System.Guid.NewGuid().ToString("N").Substring(0, 16).ToUpper();
            }
        }

        public static async Task<IResult> GetExternalTransactions(
     [FromQuery] string account,
     [FromQuery] DateTime fromDate,
     [FromQuery] DateTime toDate)
        {
            var referenceId = Guid
                .NewGuid()
                .ToString("N")
                .Substring(0, 16)
                .ToUpper();

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
            { "@TID",   referenceId },
            { "@ACC",   account },
            { "@BYDTE", "Y" },
            { "@FDATE", fromDate.ToString("yyyyMMdd") },
            { "@TDATE", toDate.ToString("yyyyMMdd")   },
            { "@BYNBR", "N" },
            { "@NBR",   "2" }
        }
            };

            using var client = new HttpClient { BaseAddress = new Uri("http://10.1.1.205:7070") };
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var resp = await client.PostAsJsonAsync("/api/mobile/transactions", payload);
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                return Results.BadRequest($"External API error ({resp.StatusCode}): {body}");

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("Details", out var details))
                return Results.Ok(new List<ExternalTransactionDto>());

            // Guard against null or non-array Transactions
            if (!details.TryGetProperty("Transactions", out var txns) ||
                 txns.ValueKind != JsonValueKind.Array)
            {
                return Results.Ok(new List<ExternalTransactionDto>());
            }

            var list = txns
                .EnumerateArray()
                .Select(el =>
                {
                    var pod = el.GetProperty("YBCD04POD").GetString()?.Trim();
                    var drcr = el.GetProperty("YBCD04DRCR").GetString();
                    var ama = el.GetProperty("YBCD04AMA").GetDecimal();

                    var narrs = new List<string>();
                    var n1 = el.GetProperty("YBCD04NAR1").GetString()?.Trim();
                    var n2 = el.GetProperty("YBCD04NAR2").GetString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(n1)) narrs.Add(n1);
                    if (!string.IsNullOrWhiteSpace(n2)) narrs.Add(n2);

                    return new ExternalTransactionDto
                    {
                        PostingDate = pod,
                        Narratives = narrs,
                        Amount = ama,
                        DrCr = drcr
                    };
                })
                .ToList();

            return Results.Ok(list);
        }






        // Helper method to generate a 16-character uppercase reference ID.
        private static string GenerateReferenceId()
        {
            return System.Guid.NewGuid().ToString("N").Substring(0, 16).ToUpper();
        }

        // DTOs for external API response.
        private class ExternalRefundResponseHeaderDto
        {
            public string System { get; set; } = string.Empty;
            public string ReferenceId { get; set; } = string.Empty;
            public string ReturnCode { get; set; } = string.Empty;
            public string ReturnMessageCode { get; set; } = string.Empty;
            public string ReturnMessage { get; set; } = string.Empty;
            public string CurCode { get; set; } = string.Empty;
            public string CurDescrip { get; set; } = string.Empty;
        }

        private class ExternalRefundResponseDetailsDto
        {
            // Define additional properties if required.
        }

        private class ExternalRefundResponseDto
        {
            public ExternalRefundResponseHeaderDto Header { get; set; } = new ExternalRefundResponseHeaderDto();
            public ExternalRefundResponseDetailsDto Details { get; set; } = new ExternalRefundResponseDetailsDto();
        }

        public class ExternalAccountsResponseHeaderDto
        {
            public string System { get; set; } = string.Empty;
            public string ReferenceId { get; set; } = string.Empty;
            public string ReturnCode { get; set; } = string.Empty;
            public string ReturnMessageCode { get; set; } = string.Empty;
            public string ReturnMessage { get; set; } = string.Empty;
            public string CurCode { get; set; } = string.Empty;
            public string CurDescrip { get; set; } = string.Empty;
        }

        public class ExternalAccountDto
        {
            // These properties match the names found in the external API response.
            // You can add more properties if you need to work with additional fields.
            public string? YBCD01AB { get; set; }
            public string? YBCD01AN { get; set; }
            public string? YBCD01AS { get; set; }
        }

        public class ExternalAccountsResponseDetailsDto
        {
            public List<ExternalAccountDto> Accounts { get; set; } = new List<ExternalAccountDto>();
        }

        public class ExternalAccountsResponseDto
        {
            public ExternalAccountsResponseHeaderDto Header { get; set; } = new ExternalAccountsResponseHeaderDto();
            public ExternalAccountsResponseDetailsDto Details { get; set; } = new ExternalAccountsResponseDetailsDto();
        }
    }
}
