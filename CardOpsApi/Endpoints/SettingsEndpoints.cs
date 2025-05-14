using CardOpsApi.Core.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CardOpsApi.Data.Abstractions;
using AutoMapper;
using CardOpsApi.Data.Models;
using CardOpsApi.Abstractions;

namespace CardOpsApi.Endpoints
{
    public class SettingsEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            var settings = app.MapGroup("/api/settings").RequireAuthorization("requireAuthUser");

            settings.MapGet("/", Get)
                .WithName("GetSettings")
                .Produces<SettingsDto>(200);

            settings.MapPatch("/", Patch) // Use PATCH for partial update
                .WithName("UpdateSettings")
                .Accepts<SettingsPatchDto>("application/json")
                .Produces<SettingsDto>(200)
                .Produces(400);
        }

        // 🔹 GET the first Settings (not by ID)
        public static async Task<IResult> Get([FromServices] ISettingsRepository settingsRepository, [FromServices] IMapper mapper)
        {
            var settings = await settingsRepository.GetFirstSettingsAsync(); // Fetch the first row
            if (settings == null) return TypedResults.NotFound("Settings not found.");

            return TypedResults.Ok(mapper.Map<SettingsDto>(settings));
        }

        // 🔹 PATCH settings (allow partial updates)

        public static async Task<IResult> Patch([FromServices] ISettingsRepository settingsRepository, [FromServices] IMapper mapper, [FromBody] SettingsPatchDto settingsDto)
        {
            var settings = await settingsRepository.GetFirstSettingsAsync(); // Fetch the first row
            if (settings == null) return TypedResults.NotFound("Settings not found.");

            // Validation for TransactionAmount: It cannot be less than 50,000


            // Validation for TransactionTimeTo: It cannot be greater than 15


            // Only update the fields that are provided
            if (settingsDto.TopAtmRefundLimit.HasValue)
                settings.TopAtmRefundLimit = settingsDto.TopAtmRefundLimit.Value;

            if (settingsDto.TopReasonLimit.HasValue)
                settings.TopReasonLimit = settingsDto.TopReasonLimit.Value;



            settingsRepository.Update(settings);
            await settingsRepository.SaveAsync();

            return TypedResults.Ok(mapper.Map<SettingsDto>(settings)); // Return updated settings
        }
    }
}

