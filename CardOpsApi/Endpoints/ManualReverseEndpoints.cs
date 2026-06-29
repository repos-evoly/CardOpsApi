using CardOpsApi.Abstractions;
using CardOpsApi.Core.Abstractions;
using CardOpsApi.Core.Dtos;
using CardOpsApi.Data.Abstractions;
using CardOpsApi.Data.Models;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;
using System.Text.Json;

namespace CardOpsApi.Endpoints
{
    public class ManualReverseEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            var group = app.MapGroup("/api/ManualReverse").RequireAuthorization("requireAuthUser");

            group.MapPost("/", ProcessManualReverse)
                .WithName("Process")
                .Accepts<ManualReverseCreateDto>("application/json")
                .Produces(200)
                .Produces(400);

            group.MapPost("/{id:int}/Execute", ExecuteManualReverse)
                .WithName("ExecuteTransfer")
                .Produces(200)
                .Produces(400)
                .Produces(404);

            group.MapGet("/", GetTransfers)
                .WithName("GetTransfers")
                .Produces<List<ManualReverseDto>>(200);
        }

        // POST /api/transfers — save only, no banking call
        public static async Task<IResult> ProcessManualReverse(
            [FromBody]     ManualReverseCreateDto           dto,
            [FromServices] IManualReverseRepository         manualReverseRepository,
            [FromServices] ILogger<ManualReverseEndpoints>  logger)
        {
            if (string.IsNullOrWhiteSpace(dto.DestinationAccount) || dto.DestinationAccount.Length != 13)
                return Results.BadRequest("DestinationAccount must be exactly 13 characters.");

            if (dto.Amount <= 0)
                return Results.BadRequest("Amount must be greater than zero.");

            if (string.IsNullOrWhiteSpace(dto.OriginalReference) && string.IsNullOrWhiteSpace(dto.RrnReference))
                return Results.BadRequest("Either Original Transaction Reference or RNN must be provided.");

            var transfer = new ManualReverse
            {
                DestinationAccount = dto.DestinationAccount,
                Amount             = dto.Amount,
                TransferStatus     = "PENDING",
                TransferReference  = null,
                OriginalReference = dto.OriginalReference ?? null,
                RrnReference = dto.RrnReference ?? null,
                ManualReverseExecuteDate = null,
                ManualReverseDate = dto.ManualReverseDate,
                ResponseMessage    = dto.ResponseMessage
            };

            await manualReverseRepository.CreateAsync(transfer);

            logger.LogInformation("[ProcessTransfer] Saved Id={Id}, Status=PENDING", transfer.Id);

            return Results.Ok(new { message = "Transfer saved successfully.", transferId = transfer.Id, transfer.TransferStatus });
        }

        // POST /api/transfers/{id}/process — trigger the banking call for a saved record
        public static async Task<IResult> ExecuteManualReverse(
            int            id,
            [FromServices] IManualReverseRepository         manualReverseRepository,
            [FromServices] ISettingsRepository         settingsRepository,
            [FromServices] IConfiguration              config,
            [FromServices] IHostEnvironment            env,
            [FromServices] HttpClient                  httpClient,
            [FromServices] ILogger<ManualReverseEndpoints>  logger)
        {
            var transfer = await manualReverseRepository.GetByIdAsync(id);

            if (transfer == null)
                return Results.NotFound($"Transfer with id {id} was not found.");

            if (transfer.TransferStatus == "SUCCESS")
                return Results.BadRequest($"Transfer {id} has already been processed successfully.");

            var settings      = await settingsRepository.GetFirstSettingsAsync();
            var sourceAccount = "0010838888434";

            var (success, message, referenceId) = await CallPostTransferAsync(
                sourceAccount, transfer.DestinationAccount, transfer.Amount,
                config, env, httpClient, logger);

            await manualReverseRepository.UpdateStatusAsync(id,
                success ? "SUCCESS" : "FAILED",
                referenceId,
                message);

            logger.LogInformation(
                "[ExecuteTransfer] Id={Id}, Status={Status}, Ref={Ref}",
                id, success ? "SUCCESS" : "FAILED", referenceId);

            if (!success)
                return Results.BadRequest(new { message, referenceId, TransferStatus = "FAILED" });

            return Results.Ok(new { message, referenceId, TransferStatus = "SUCCESS" });
        }

        // GET /api/transfers — return all saved records
// GET /api/ManualReverse?fromDate=&toDate=&keyword=&page=&limit=
        public static async Task<IResult> GetTransfers(
            [FromServices] IManualReverseRepository transferRepository,
            [FromQuery]    DateTime?                fromDate = null,
            [FromQuery]    DateTime?                toDate   = null,
            [FromQuery]    string?                  keyword  = null,
            [FromQuery]    string?                  accountNumber = null,
            [FromQuery]    int                      page     = 1,
            [FromQuery]    int                      limit    = 100000)
        {
            if (page < 1)
                return Results.BadRequest("page must be greater than or equal to 1.");

            if (limit < 1)
                return Results.BadRequest("limit must be greater than or equal to 1.");

            var transfers  = await transferRepository.GetAllAsync(fromDate, toDate, keyword,accountNumber, page, limit);
            var totalCount = await transferRepository.GetCountAsync(fromDate, toDate, keyword, accountNumber);
            var totalPages = (int)Math.Ceiling((double)totalCount / limit);

            return Results.Ok(new
            {
                Data       = transfers,
                TotalCount = totalCount,
                TotalPages = totalPages,
                Page       = page,
                Limit      = limit
            });
        }

        // Shared helper — identical behaviour to FisBankRecordEndpoints
        private static async Task<(bool success, string message, string referenceId)> CallPostTransferAsync(
            string           sourceAccount,
            string           destinationAccount,
            decimal          amount,
            IConfiguration   config,
            IHostEnvironment env,
            HttpClient       httpClient,
            ILogger          logger)
        {
            var baseUrl = env.IsProduction()
                ? config["CoreApi:BaseUrlProd"]    ?? "http://10.1.1.205:7070"
                : config["CoreApi:BaseUrlNonProd"] ?? "http://10.3.3.11:7070";

            const int DEC    = 3;
            long amountUnits = (long)Math.Round(amount * (decimal)Math.Pow(10, DEC), MidpointRounding.AwayFromZero);
            var refId        = Guid.NewGuid().ToString("N")[..16].ToUpper();

            var requestObj = new
            {
                Header = new
                {
                    system         = "MOBILE",
                    referenceId    = refId,
                    userName       = "TEDMOB",
                    customerNumber = destinationAccount,
                    requestTime    = DateTime.UtcNow.ToString("o"),
                    language       = "AR"
                },

                Details = new Dictionary<string, string>
                {
                    ["@TRFCCY"]   = "LYD",
                    ["@SRCACC"]   = sourceAccount,
                    ["@DSTACC"]   = destinationAccount,
                    ["@DSTACC2"]  = string.Empty,
                    ["@TRFAMT"]   = amountUnits.ToString(),
                    ["@APLYTRN2"] = "N",
                    ["@TRFAMT2"]  = new string('0', 15),
                    ["@NR2"]      = $"Transfer to {destinationAccount}"
                }
            };

            logger.LogInformation("[Transfer] Sending ref={Ref}: {Payload}",
                refId, JsonSerializer.Serialize(requestObj));

            try
            {
                var response = await httpClient.PostAsJsonAsync($"{baseUrl}/api/mobile/postTransfer", requestObj);
                var raw      = await response.Content.ReadAsStringAsync();
                logger.LogInformation("[Transfer] Response ref={Ref}: {Raw}", refId, raw);

                if (!response.IsSuccessStatusCode)
                    return (false, $"External API returned {response.StatusCode}", refId);

                var respObj = JsonSerializer.Deserialize<PostTransferResponse>(
                    raw, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (respObj?.Header == null)
                    return (false, "Malformed response from external API.", refId);

                bool ok = string.Equals(respObj.Header.ReturnCode, "Success", StringComparison.OrdinalIgnoreCase);
                return (ok, respObj.Header.ReturnMessage, refId);
            }
            catch (HttpRequestException ex)
            {
                logger.LogError(ex, "[Transfer] HTTP error ref={Ref}", refId);
                return (false, $"HTTP error: {ex.Message}", refId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Transfer] Unexpected error ref={Ref}", refId);
                return (false, $"Error: {ex.Message}", refId);
            }
        }

        private class PostTransferResponse  { public PostTransferHeader? Header { get; set; } }
        private class PostTransferHeader    { public string ReturnCode { get; set; } = string.Empty; public string ReturnMessage { get; set; } = string.Empty; }
    }
}