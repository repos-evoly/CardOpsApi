using CardOpsApi.Core.Abstractions;
using CardOpsApi.Core.Dtos;
using CardOpsApi.Data.Models;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardOpsApi.Abstractions;

namespace CardOpsApi.Endpoints
{
    public class ReasonEndpoints : IEndpoints
    {
        public void RegisterEndpoints(WebApplication app)
        {
            var reasons = app.MapGroup("/api/reasons").RequireAuthorization("requireAuthUser");

            reasons.MapGet("/", GetAll);
            reasons.MapGet("/{id:int}", GetById);
            reasons.MapPost("/", Create);
            reasons.MapPut("/{id:int}", Update);
            reasons.MapDelete("/{id:int}", Delete);
        }

        private static async Task<IResult> GetAll(
           [FromServices] IReasonRepository repo,
           [FromServices] IMapper mapper,
           [FromQuery] string? searchTerm,
           [FromQuery] string? searchBy,
           [FromQuery] int page = 1,
           [FromQuery] int limit = 100000)
        {
            // Retrieve the page data.
            var data = await repo.GetAllAsync(searchTerm, searchBy, page, limit);
            var dto = mapper.Map<List<ReasonDto>>(data);

            // Retrieve the total number of matching records.
            int totalRecords = await repo.GetCountAsync(searchTerm, searchBy);
            int totalPages = (int)System.Math.Ceiling((double)totalRecords / limit);

            return Results.Ok(new { Data = dto, TotalPages = totalPages });
        }

        private static async Task<IResult> GetById(int id, [FromServices] IReasonRepository repo, [FromServices] IMapper mapper)
        {
            var item = await repo.GetByIdAsync(id);
            if (item == null) return Results.NotFound("Reason not found.");
            return Results.Ok(mapper.Map<ReasonDto>(item));
        }

        private static async Task<IResult> Create([FromBody] ReasonCreateDto dto, [FromServices] IReasonRepository repo, [FromServices] IMapper mapper)
        {
            var model = mapper.Map<Reason>(dto);
            await repo.CreateAsync(model);
            return Results.Created($"/api/reasons/{model.Id}", mapper.Map<ReasonDto>(model));
        }

        private static async Task<IResult> Update(int id, [FromBody] ReasonUpdateDto dto, [FromServices] IReasonRepository repo, [FromServices] IMapper mapper)
        {
            var model = await repo.GetByIdAsync(id);
            if (model == null) return Results.NotFound("Reason not found.");
            mapper.Map(dto, model);
            await repo.UpdateAsync(model);
            return Results.Ok(mapper.Map<ReasonDto>(model));
        }

        private static async Task<IResult> Delete(int id, [FromServices] IReasonRepository repo)
        {
            await repo.DeleteAsync(id);
            return Results.Ok("Reason deleted.");
        }
    }
}
