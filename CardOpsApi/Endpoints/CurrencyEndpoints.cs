using CardOpsApi.Core.Abstractions;
using CardOpsApi.Core.Dtos;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardOpsApi.Data.Models;
using FluentValidation;
using CardOpsApi.Abstractions;

namespace CardOpsApi.Endpoints
{
    public class CurrencyEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            var currencies = app.MapGroup("/api/currencies").RequireAuthorization("requireAuthUser");

            currencies.MapGet("/", GetCurrencies)
                .WithName("GetCurrencies")
                .Produces<List<CurrencyDto>>(200);

            currencies.MapGet("/{id:int}", GetCurrencyById)
                .WithName("GetCurrencyById")
                .Produces<CurrencyDto>(200)
                .Produces(404);

            currencies.MapPost("/", CreateCurrency)
                .WithName("CreateCurrency")
                .Accepts<CurrencyCreateDto>("application/json")
                .Produces<CurrencyDto>(201)
                .Produces(400);

            currencies.MapPut("/{id:int}", UpdateCurrency)
                .WithName("UpdateCurrency")
                .Accepts<CurrencyUpdateDto>("application/json")
                .Produces<CurrencyDto>(200)
                .Produces(400)
                .Produces(404);

            currencies.MapDelete("/{id:int}", DeleteCurrency)
                .WithName("DeleteCurrency")
                .Produces(200)
                .Produces(404);
        }

        public static async Task<IResult> GetCurrencies(
            [FromServices] ICurrencyRepository currencyRepository,
            [FromServices] IMapper mapper,
            [FromQuery] string? searchTerm,
            [FromQuery] string? searchBy,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 100000)
        {
            var currencies = await currencyRepository.GetAllAsync(searchTerm, searchBy, page, limit);
            var currencyDtos = mapper.Map<List<CurrencyDto>>(currencies);

            int totalRecords = await currencyRepository.GetCountAsync(searchTerm, searchBy);
            int totalPages = (int)System.Math.Ceiling((double)totalRecords / limit);

            return Results.Ok(new { Data = currencyDtos, TotalPages = totalPages });

        }

        public static async Task<IResult> GetCurrencyById(
            int id,
            [FromServices] ICurrencyRepository currencyRepository,
            [FromServices] IMapper mapper)
        {
            var currency = await currencyRepository.GetByIdAsync(id);
            if (currency == null)
            {
                return Results.NotFound("Currency not found.");
            }
            var dto = mapper.Map<CurrencyDto>(currency);
            return Results.Ok(dto);
        }

        public static async Task<IResult> CreateCurrency(
            [FromBody] CurrencyCreateDto createDto,
            [FromServices] ICurrencyRepository currencyRepository,
            [FromServices] IMapper mapper,
            [FromServices] IValidator<CurrencyCreateDto> validator)
        {
            var validationResult = await validator.ValidateAsync(createDto);
            if (!validationResult.IsValid)
            {
                return Results.BadRequest(validationResult.Errors.Select(e => e.ErrorMessage));
            }
            var currency = mapper.Map<Currency>(createDto);
            await currencyRepository.CreateAsync(currency);
            var dto = mapper.Map<CurrencyDto>(currency);
            return Results.Created($"/api/currencies/{dto.Id}", dto);
        }

        public static async Task<IResult> UpdateCurrency(
            int id,
            [FromBody] CurrencyUpdateDto updateDto,
            [FromServices] ICurrencyRepository currencyRepository,
            [FromServices] IMapper mapper,
            [FromServices] IValidator<CurrencyUpdateDto> validator)
        {
            var existingCurrency = await currencyRepository.GetByIdAsync(id);
            if (existingCurrency == null)
            {
                return Results.NotFound("Currency not found.");
            }
            var validationResult = await validator.ValidateAsync(updateDto);
            if (!validationResult.IsValid)
            {
                return Results.BadRequest(validationResult.Errors.Select(e => e.ErrorMessage));
            }
            mapper.Map(updateDto, existingCurrency);
            await currencyRepository.UpdateAsync(existingCurrency);
            var dto = mapper.Map<CurrencyDto>(existingCurrency);
            return Results.Ok(dto);
        }

        public static async Task<IResult> DeleteCurrency(
            int id,
            [FromServices] ICurrencyRepository currencyRepository)
        {
            var existingCurrency = await currencyRepository.GetByIdAsync(id);
            if (existingCurrency == null)
            {
                return Results.NotFound("Currency not found.");
            }
            await currencyRepository.DeleteAsync(id);
            return Results.Ok("Currency deleted successfully.");
        }
    }
}
