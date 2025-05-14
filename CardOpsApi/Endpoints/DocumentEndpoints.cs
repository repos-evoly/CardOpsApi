// using CardOpsApi.Core.Abstractions;
// using CardOpsApi.Core.Dtos;
// using Microsoft.AspNetCore.Mvc;
// using Microsoft.AspNetCore.Http;
// using Microsoft.Extensions.Logging;
// using System;
// using System.Collections.Generic;
// using System.Threading.Tasks;
// using CardOpsApi.Abstractions;

// namespace CardOpsApi.Endpoints
// {
//     public class DocumentEndpoints : IEndpoints
//     {
//         public void RegisterEndpoints(WebApplication app)
//         {
//             var documents = app.MapGroup("/api/documents").RequireAuthorization("requireAuthUser");

//             // ✅ Upload a document
//             documents.MapPost("/upload", UploadDocument)
//                 .WithName("UploadDocument")
//                 .Accepts<IFormFile>("multipart/form-data")
//                 .Produces(201)
//                 .Produces(400);

//             // ✅ Get all documents by type (reports, bank documents)
//             documents.MapGet("/{documentType}", GetDocuments)
//                 .WithName("GetDocuments")
//                 .Produces<List<DocumentResponseDto>>(200)
//                 .Produces(400);

//             // ✅ Get document details by ID
//             documents.MapGet("/details/{id:guid}", GetDocumentById)
//                 .WithName("GetDocumentById")
//                 .Produces<DocumentResponseDto>(200)
//                 .Produces(404);

//             // ✅ Delete a document
//             documents.MapDelete("/{id:guid}", DeleteDocument)
//                 .WithName("DeleteDocument")
//                 .Produces(200)
//                 .Produces(404);
//         }

//         public static async Task<IResult> UploadDocument(
//             [FromServices] IDocumentRepository documentRepository,
//             HttpRequest request,
//             ILogger<DocumentEndpoints> logger)
//         {
//             if (!request.Form.Files.Any())
//                 return Results.BadRequest("No file was uploaded.");

//             var file = request.Form.Files[0];
//             if (file.Length == 0)
//                 return Results.BadRequest("Uploaded file is empty.");

//             // ✅ Retrieve form data
//             var title = request.Form["Title"].ToString();
//             var description = request.Form["Description"].ToString();
//             var documentType = request.Form["DocumentType"].ToString();
//             if (string.IsNullOrWhiteSpace(request.Form["UploadedByUserId"]))
//                 return Results.BadRequest("UploadedByUserId is required.");

//             var uploadedByUserId = int.Parse(request.Form["UploadedByUserId"]!);

//             if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(documentType))
//                 return Results.BadRequest("Title and DocumentType are required.");

//             var documentDto = new DocumentDto
//             {
//                 Title = title,
//                 Description = description,
//                 DocumentType = documentType,
//                 UploadedByUserId = uploadedByUserId
//             };

//             bool uploaded = await documentRepository.UploadDocument(file, documentDto);
//             return uploaded ? Results.Created("Document uploaded successfully.", null) : Results.BadRequest("Failed to upload document.");
//         }

//         public static async Task<IResult> GetDocuments(
//             [FromQuery] string? documentType,
//             [FromQuery] string? searchBy,
//             [FromQuery] string? query,
//             [FromServices] IDocumentRepository documentRepository,
//             [FromServices] ILogger<DocumentEndpoints> logger,
//             [FromQuery] int page = 1,
//             [FromQuery] int limit = 100000000)
//         {
//             IEnumerable<DocumentResponseDto> documents;

//             // If both searchBy and query are provided, use the search method.
//             if (!string.IsNullOrWhiteSpace(searchBy) && !string.IsNullOrWhiteSpace(query))
//             {
//                 documents = await documentRepository.SearchDocuments(searchBy, query);

//                 // If a documentType filter is also provided, apply it.
//                 if (!string.IsNullOrWhiteSpace(documentType))
//                 {
//                     documents = documents.Where(d => d.DocumentType.Equals(documentType, System.StringComparison.OrdinalIgnoreCase));
//                 }
//             }
//             else if (!string.IsNullOrWhiteSpace(documentType))
//             {
//                 // No search query provided; filter by documentType.
//                 documents = await documentRepository.GetDocuments(documentType);
//             }
//             else
//             {
//                 // If no filters are provided, call search with an empty query.
//                 documents = await documentRepository.SearchDocuments("title", "");
//             }

//             // Apply pagination.
//             var pagedDocuments = documents
//                 .Skip((page - 1) * limit)
//                 .Take(limit)
//                 .ToList();

//             if (pagedDocuments.Any())
//             {
//                 logger.LogInformation("Returning {Count} documents for page {Page} with limit {Limit}.", pagedDocuments.Count, page, limit);
//                 return Results.Ok(pagedDocuments);
//             }
//             else
//             {
//                 return Results.NotFound("No documents found matching the criteria.");
//             }
//         }


//         public static async Task<IResult> GetDocumentById(
//             [FromRoute] Guid id,
//             [FromServices] IDocumentRepository documentRepository,
//             ILogger<DocumentEndpoints> logger)
//         {
//             var document = await documentRepository.GetDocumentById(id);
//             return document != null ? Results.Ok(document) : Results.NotFound("Document not found.");
//         }

//         public static async Task<IResult> DeleteDocument(
//             [FromRoute] Guid id,
//             [FromServices] IDocumentRepository documentRepository,
//             ILogger<DocumentEndpoints> logger)
//         {
//             bool deleted = await documentRepository.DeleteDocument(id);
//             return deleted ? Results.Ok("Document deleted successfully.") : Results.NotFound("Document not found.");
//         }
//     }
// }
