using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using CardOpsApi.Core.Abstractions;
using CardOpsApi.Core.Dtos;
using CardOpsApi.Data.Models;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CardOpsApi.Abstractions;

namespace CardOpsApi.Endpoints
{
    public class DefinitionEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            // Require a specific policy for definitions – adjust the policy name as needed.
            var definitions = app.MapGroup("/api/definitions").RequireAuthorization("requireAuthUser");

            definitions.MapGet("/", GetDefinitions)
                .WithName("GetDefinitions")
                .Produces<List<DefinitionDto>>(200);

            definitions.MapGet("/{id:int}", GetDefinitionById)
                .WithName("GetDefinitionById")
                .Produces<DefinitionDto>(200)
                .Produces(404);

            definitions.MapPost("/", CreateDefinition)
                .WithName("CreateDefinition")
                .Accepts<DefinitionCreateDto>("application/json")
                .Produces<DefinitionDto>(201)
                .Produces(400);

            definitions.MapPut("/{id:int}", UpdateDefinition)
                .WithName("UpdateDefinition")
                .Accepts<DefinitionUpdateDto>("application/json")
                .Produces<DefinitionDto>(200)
                .Produces(400)
                .Produces(404);

            definitions.MapDelete("/{id:int}", DeleteDefinition)
                .WithName("DeleteDefinition")
                .Produces(200)
                .Produces(404);
        }

        // GET /api/definitions?searchTerm=&searchBy=&type=&page=&limit=
        public static async Task<IResult> GetDefinitions(
           [FromServices] IDefinitionRepository definitionRepository,
           [FromServices] IMapper mapper,
           [FromQuery] string? searchTerm,
           [FromQuery] string? searchBy,
           [FromQuery] string? type,
           [FromQuery] int page = 1,
           [FromQuery] int limit = 100000)
        {
            var definitions = await definitionRepository.GetAllAsync(searchTerm, searchBy, type, page, limit);
            var definitionDtos = mapper.Map<List<DefinitionDto>>(definitions);

            // Get the total record count.
            int totalRecords = await definitionRepository.GetCountAsync(searchTerm, searchBy, type);
            int totalPages = (int)System.Math.Ceiling((double)totalRecords / limit);

            return Results.Ok(new { Data = definitionDtos, TotalPages = totalPages });
        }

        // GET /api/definitions/{id}
        public static async Task<IResult> GetDefinitionById(
            int id,
            [FromServices] IDefinitionRepository definitionRepository,
            [FromServices] IMapper mapper)
        {
            var definition = await definitionRepository.GetByIdAsync(id);
            if (definition == null)
            {
                return Results.NotFound("Definition not found.");
            }
            var dto = mapper.Map<DefinitionDto>(definition);
            return Results.Ok(dto);
        }

        // POST /api/definitions
        public static async Task<IResult> CreateDefinition(
            [FromBody] DefinitionCreateDto createDto,
            [FromServices] IDefinitionRepository definitionRepository,
            [FromServices] IMapper mapper,
            [FromServices] IValidator<DefinitionCreateDto> validator)
        {
            ValidationResult validationResult = await validator.ValidateAsync(createDto);
            if (!validationResult.IsValid)
            {
                return Results.BadRequest(validationResult.Errors.Select(e => e.ErrorMessage));
            }

            var definition = mapper.Map<Definition>(createDto);
            await definitionRepository.CreateAsync(definition);
            var dto = mapper.Map<DefinitionDto>(definition);
            return Results.Created($"/api/definitions/{dto.Id}", dto);
        }

        // PUT /api/definitions/{id}
        public static async Task<IResult> UpdateDefinition(
            int id,
            [FromBody] DefinitionUpdateDto updateDto,
            [FromServices] IDefinitionRepository definitionRepository,
            [FromServices] IMapper mapper,
            [FromServices] IValidator<DefinitionUpdateDto> validator)
        {
            var existingDefinition = await definitionRepository.GetByIdAsync(id);
            if (existingDefinition == null)
            {
                return Results.NotFound("Definition not found.");
            }

            ValidationResult validationResult = await validator.ValidateAsync(updateDto);
            if (!validationResult.IsValid)
            {
                return Results.BadRequest(validationResult.Errors.Select(e => e.ErrorMessage));
            }

            mapper.Map(updateDto, existingDefinition);
            await definitionRepository.UpdateAsync(existingDefinition);
            var dto = mapper.Map<DefinitionDto>(existingDefinition);
            return Results.Ok(dto);
        }

        // DELETE /api/definitions/{id}
        public static async Task<IResult> DeleteDefinition(
            int id,
            [FromServices] IDefinitionRepository definitionRepository)
        {
            var existingDefinition = await definitionRepository.GetByIdAsync(id);
            if (existingDefinition == null)
            {
                return Results.NotFound("Definition not found.");
            }
            await definitionRepository.DeleteAsync(id);
            return Results.Ok("Definition deleted successfully.");
        }
    }
}
