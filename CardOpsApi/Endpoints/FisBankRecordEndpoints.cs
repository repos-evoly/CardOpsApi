using AutoMapper;
using CardOpsApi.Abstractions;
using CardOpsApi.Core.Abstractions;
using CardOpsApi.Core.Dtos;
using CardOpsApi.Data.Abstractions;
using CardOpsApi.Data.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;
using System.Text.Json;

namespace CardOpsApi.Endpoints
{
    public class FisBankRecordEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            var group = app.MapGroup("/api/fis-bank-records").RequireAuthorization("requireAuthUser");

            // 1. Import from Excel upload
            group.MapPost("/import", ImportFromExcel)
                .WithName("ImportFisBankRecords")
                .Accepts<IFormFile>("multipart/form-data")
                .Produces<FisBankRecordImportResultDto>(200)
                .Produces(400)
                .DisableAntiforgery();

            // 2. Process transfer for a single record by id
            group.MapPost("/{id:int}/process-transfer", ProcessSingleTransfer)
                .WithName("ProcessSingleFisTransfer")
                .Produces(200)
                .Produces(400)
                .Produces(404);

            // 3. Process transfer for all records
            group.MapPost("/{fileId:int}/process-all-transfers", ProcessAllTransfers)
                .WithName("ProcessAllFisTransfers")
                .Produces(200)
                .Produces(400);

            // 4. Get all imported Excel files
            group.MapGet("/files", GetFiles)
                .WithName("GetFisBankFiles")
                .Produces<List<FisBankFileDto>>(200);

            // 5. Get one imported Excel file with its contents
            group.MapGet("/files/{id:int}", GetFile)
                .WithName("GetFisBankFile")
                .Produces<FisBankFileDto>(200)
                .Produces(404);

            // 6. Get records belonging to a specific imported file
            group.MapGet("/files/{id:int}/records", GetFileRecords)
                .WithName("GetFisBankFileRecords")
                .Produces<List<FisBankRecordDto>>(200)
                .Produces(404);

            group.MapPost("/delete-records/{id}", SoftDeleteRecord)
                .WithName("SoftDelete")
                .Produces(200)
                .Produces(400);
        }

        public static async Task<IResult> GetFiles(
            [FromServices] IFisBankRecordRepository repository,
            [FromQuery] string? search = null,
            [FromQuery] DateTime? dateFrom = null,
            [FromQuery] DateTime? dateTo = null)
        {
            var files = await repository.GetFilesAsync(search, dateFrom, dateTo);
            return Results.Ok(files);
        }

        public static async Task<IResult> GetFile(
            int id,
            [FromServices] IFisBankRecordRepository repository)
        {
            var file = await repository.GetFileAsync(id);

            if (file == null)
                return Results.NotFound(
                    $"Fis bank file with id {id} was not found.");

            return Results.Ok(file);
        }

        public static async Task<IResult> GetFileRecords(
            int id,
            [FromServices] IFisBankRecordRepository repository)
        {
            var records = await repository.GetFileRecordsAsync(id);

            return Results.Ok(records);
        }

        public static async Task<IResult> SoftDeleteRecord(
            int id,
            [FromServices] IFisBankRecordRepository repo,
            [FromServices] ILogger<FisBankRecordEndpoints> logger)
        {
            var record = await repo.GetByIdAsync(id);

            if (record == null)
                return Results.NotFound($"FIS bank record with id {id} was not found.");

            if (record.TransferStatus == "SUCCESS")
                return Results.BadRequest(
                    $"Record {id} has been successfully processed and cannot be deleted.");

            await repo.SoftDeleteAsync(id);

            logger.LogInformation("[SoftDeleteFisBankRecord] Record {Id} soft-deleted.", id);

            return Results.Ok($"Record {id} has been deleted successfully.");
        }

        // ─────────────────────────────────────────────────────────────
        // POST /api/fis-bank-records/import
        // Accepts a multipart Excel file, parses every row and saves them
        // ─────────────────────────────────────────────────────────────
        public static async Task<IResult> ImportFromExcel(
            HttpRequest request,
            [FromServices] IFisBankRecordRepository repo,
            [FromServices] ILogger<FisBankRecordEndpoints> logger)
        {
            if (!request.HasFormContentType || request.Form.Files.Count == 0)
                return Results.BadRequest("No file was uploaded. Please send a multipart/form-data request with a file field.");

            var file = request.Form.Files[0];
            if (file.Length == 0)
                return Results.BadRequest("Uploaded file is empty.");

            var allowedExtensions = new[] { ".xlsx", ".xls" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
                return Results.BadRequest("Only .xlsx or .xls files are accepted.");

            logger.LogInformation("[ImportFisBankRecords] Importing file: {FileName}, size: {Size} bytes", file.FileName, file.Length);

            var canImport = await repo.CanImportFileAsync(file.FileName);
            if (!canImport)
                return Results.BadRequest(
                    $"File '{file.FileName}' has already been imported and contains one or more " +
                    "successfully processed records. Re-import is not allowed.");

            using var stream = file.OpenReadStream();
            var result = await repo.ImportFromExcelAsync(stream, file.FileName);

            logger.LogInformation("[ImportFisBankRecords] Imported {Count} rows, skipped {Skipped}",
                result.ImportedCount, result.SkippedCount);

            return Results.Ok(result);
        }

        // ─────────────────────────────────────────────────────────────
        // POST /api/fis-bank-records/{id}/process-transfer
        // Calls the core banking postTransfer API for a single record,
        // stores the reference id and status on that record
        // ─────────────────────────────────────────────────────────────
        public static async Task<IResult> ProcessSingleTransfer(
            int id,
            [FromServices] ISettingsRepository settingsrepo,
            [FromServices] IFisBankRecordRepository repo,
            [FromServices] IConfiguration config,
            [FromServices] IHostEnvironment env,
            [FromServices] HttpClient httpClient,
            [FromServices] ILogger<FisBankRecordEndpoints> logger)
        {
            var record = await repo.GetByIdAsync(id);
            var db = GetDbContext(repo);

            if (record == null)
                return Results.NotFound($"FIS bank record with id {id} was not found.");

            if (record.TransferStatus == "SUCCESS")
                return Results.BadRequest($"Record {id} already has a transfer reference: {record.TransferReference}. It has already been processed.");

            if (record.IsDeleted == true)
                return Results.NotFound($"FIS bank record with id {id} was not found.");


            var (success, message, referenceId) = await CallPostTransferAsync(settingsrepo, record, config, env, httpClient, logger);

            // Always persist the reference and status, even on failure, so we have an audit trail
            var tracked = await repo.GetByIdAsync(id); // fresh copy for update (non-tracked above)
            // Re-fetch as tracked entity
            // GetByIdAsync uses AsNoTracking so we need a direct attach approach:
            record.TransferStatus    = success ? "SUCCESS" : "FAILED";
            record.TransferReference = referenceId;
            record.FailureReason     = success ? null : message; // null on success, message on failure

            // Detach and re-attach so EF can track the update

            if (db != null)
            {
                db.FisBankRecords.Attach(record);
                db.Entry(record).Property(r => r.TransferStatus).IsModified    = true;
                db.Entry(record).Property(r => r.TransferReference).IsModified = true;
                db.Entry(record).Property(r => r.FailureReason).IsModified = true;
                await db.SaveChangesAsync();
            }
            else
            {
                await repo.UpdateAsync(record);
            }

            if (!success)
                return Results.BadRequest(new { message, referenceId, record.TransferStatus });

            return Results.Ok(new { message, referenceId, record.TransferStatus });
        }

        // ─────────────────────────────────────────────────────────────
        // POST /api/fis-bank-records/process-all-transfers
        // Iterates every unprocessed record and calls the core API for each
        // ─────────────────────────────────────────────────────────────
        public static async Task<IResult> ProcessAllTransfers(
                    int fileId,
                    [FromServices] ISettingsRepository settingsrepo,
                    [FromServices] IFisBankRecordRepository repo,
                    [FromServices] IConfiguration config,
                    [FromServices] IHostEnvironment env,
                    [FromServices] HttpClient httpClient,
                    [FromServices] ILogger<FisBankRecordEndpoints> logger)
                    {
                var file = await repo.GetFileAsync(fileId);
                if (file == null)
                    return Results.NotFound($"FIS bank file with id {fileId} was not found.");

                // Load only records that belong to the selected file
                var fileRecords = await repo.GetAllByFileIdAsync(fileId);
                var unprocessed = fileRecords.Where(r => r.TransferStatus != "SUCCESS").ToList();

                if (unprocessed.Count == 0)
                    return Results.Ok(new { message = "All records for this file have already been processed.", processed = 0 });

                var results = new List<object>();
                int successCount = 0, failCount = 0;

                var db = GetDbContext(repo);

                foreach (var record in unprocessed)
                {
                    if (record.IsDeleted == true)
                        continue;
                    
                    var (success, message, referenceId) = await CallPostTransferAsync(settingsrepo, record, config, env, httpClient, logger);

                    record.TransferStatus    = success ? "SUCCESS" : "FAILED";
                    record.TransferReference = referenceId;
                    record.FailureReason     = success ? null : message; // null on success, message on failure

                    if (db != null)
                    {
                        db.FisBankRecords.Attach(record);
                        db.Entry(record).Property(r => r.TransferStatus).IsModified    = true;
                        db.Entry(record).Property(r => r.TransferReference).IsModified = true;
                        db.Entry(record).Property(r => r.FailureReason).IsModified     = true;
                    }
                    else
                    {
                        await repo.UpdateAsync(record);
                    }

                    if (success) successCount++; else failCount++;
                    results.Add(new { record.Id, record.MerchantNo, record.TransferStatus, record.TransferReference, message });
                }

                if (db != null)
                    await db.SaveChangesAsync(); // single bulk save for all records

                return Results.Ok(new
                {
                    FileId         = fileId,
                    TotalProcessed = unprocessed.Count,
                    Succeeded      = successCount,
                    Failed         = failCount,
                    Results        = results
                });
            }
        // ─────────────────────────────────────────────────────────────
        // Shared: calls /api/mobile/postTransfer exactly like
        // TransactionRepository.PostTransferAsync does.
        // Uses BankingAccountNo as both source and destination to
        // represent a settlement transfer for the merchant.
        // ─────────────────────────────────────────────────────────────
        private static async Task<(bool success, string message, string referenceId)> CallPostTransferAsync(
            ISettingsRepository repo,
            Data.Models.FisBankRecord record,
            IConfiguration config,
            IHostEnvironment env,
            HttpClient httpClient,
            ILogger logger)
        {
            var baseUrl = env.IsProduction()
                ? config["CoreApi:BaseUrlProd"]  ?? "http://10.1.1.205:7070"
                : config["CoreApi:BaseUrlNonProd"] ?? "http://10.3.3.11:7070";

            // Amount is formatted as integer units with 3 implied decimal places (same as existing code)
            const int DEC = 3;
            decimal amount     = record.TotalAmount;
            long amountUnits   = (long)Math.Round(amount * (decimal)Math.Pow(10, DEC), MidpointRounding.AwayFromZero);
            string formattedAmt = amountUnits.ToString("D15");

            var refId = Guid.NewGuid().ToString("N").Substring(0, 16).ToUpper();
            var settings = await repo.GetFirstSettingsAsync(); // Fetch the first row


            var requestObj = new
            {
                Header = new
                {
                    system         = "MOBILE",
                    referenceId    = refId,
                    userName       = "TEDMOB",
                    customerNumber = record.BankingAccountNo ?? string.Empty,
                    requestTime    = DateTime.UtcNow.ToString("o"),
                    language       = "AR"
                },
                Details = new Dictionary<string, string>
                {
                    ["@TRFCCY"] = "LYD",
                    ["@SRCACC"] = settings.FisBankAccount.ToString() ?? string.Empty,
                    ["@DSTACC"] = record.BankingAccountNo ?? string.Empty,
                    ["@DSTACC2"] = string.Empty,
                    ["@TRFAMT"] = formattedAmt,
                    ["@APLYTRN2"] = "N",
                    ["@TRFAMT2"] = new string('0', 15),
                    ["@NR2"]    = $"FIS Settlement {record.MerchantNo}"
                }
            };

            var payloadJson = JsonSerializer.Serialize(requestObj);
            logger.LogInformation("[FisTransfer] Sending transfer for record {Id}, ref {Ref}: {Payload}", record.Id, refId, payloadJson);

            try
            {
                var response = await httpClient.PostAsJsonAsync($"{baseUrl}/api/mobile/postTransfer", requestObj);
                var raw      = await response.Content.ReadAsStringAsync();
                logger.LogInformation("[FisTransfer] Response for record {Id}: {Raw}", record.Id, raw);

                if (!response.IsSuccessStatusCode)
                    return (false, $"External API returned {response.StatusCode}", refId);

                var respObj = JsonSerializer.Deserialize<PostTransferResponse>(raw, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (respObj?.Header == null)
                    return (false, "Malformed response from external API.", refId);

                bool ok = string.Equals(respObj.Header.ReturnCode, "Success", StringComparison.OrdinalIgnoreCase);
                return (ok, respObj.Header.ReturnMessage, refId);
            }
            catch (HttpRequestException ex)
            {
                logger.LogError(ex, "[FisTransfer] HTTP error for record {Id}", record.Id);
                return (false, $"HTTP error: {ex.Message}", refId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[FisTransfer] Unexpected error for record {Id}", record.Id);
                return (false, $"Error: {ex.Message}", refId);
            }
        }

        // Helper to reach the DbContext through the repository for bulk-save efficiency.
        // The repo is registered as Scoped so the context inside it is the same instance.
        private static CardOpsApiDbContext? GetDbContext(IFisBankRecordRepository repo)
        {
            if (repo is CardOpsApi.Core.Repositories.FisBankRecordRepository concrete)
            {
                // Access via reflection — avoids exposing DbContext on the interface
                var field = typeof(CardOpsApi.Core.Repositories.FisBankRecordRepository)
                    .GetField("_context", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                return field?.GetValue(concrete) as CardOpsApiDbContext;
            }
            return null;
        }

        // ─── Local response shape for the external API ───────────────
        private class PostTransferResponse
        {
            public PostTransferHeader? Header { get; set; }
        }
        private class PostTransferHeader
        {
            public string ReturnCode    { get; set; } = string.Empty;
            public string ReturnMessage { get; set; } = string.Empty;
        }
    }
}