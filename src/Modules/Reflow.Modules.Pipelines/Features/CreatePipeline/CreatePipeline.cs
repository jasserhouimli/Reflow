using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Reflow.Infrastructure.Results;
using Reflow.Modules.Pipelines.Domain;
using Reflow.Modules.Pipelines.Persistence;

namespace Reflow.Modules.Pipelines.Features.CreatePipeline;

public record CreatePipelineRequest(string Name);

public class CreatePipelineRequestValidator : AbstractValidator<CreatePipelineRequest>
{
    public CreatePipelineRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public record CreatePipelineResponse(Guid Id, string Name);

public class CreatePipelineHandler(PipelinesDbContext db, IValidator<CreatePipelineRequest> validator)
{
    public async Task<Result<CreatePipelineResponse>> Handle(
        CreatePipelineRequest request, Guid ownerId, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return Result<CreatePipelineResponse>.Failure(validation.Errors[0].ErrorMessage, 400);

        var pipeline = new Pipeline
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Name = request.Name.Trim(),
            Status = PipelineStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Pipelines.Add(pipeline);
        await db.SaveChangesAsync(ct);
        return Result<CreatePipelineResponse>.Success(new(pipeline.Id, pipeline.Name), 201);
    }
}

public static class CreatePipelineEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/v1/pipelines", async (
            CreatePipelineHandler handler,
            CreatePipelineRequest request,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (!PipelineAuth.TryOwnerId(http.User, out var ownerId))
                return Results.Json(new { error = "Unauthorized" }, statusCode: 401);
            var result = await handler.Handle(request, ownerId, ct);
            return result.IsSuccess
                ? Results.Json(result.Value, statusCode: result.StatusCode)
                : Results.Json(new { error = result.Error }, statusCode: result.StatusCode);
        }).RequireAuthorization().WithName("CreatePipeline").Produces(201).Produces(400);
    }
}
