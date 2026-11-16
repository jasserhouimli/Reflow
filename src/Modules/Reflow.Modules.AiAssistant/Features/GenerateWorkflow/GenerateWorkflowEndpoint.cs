using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Reflow.Infrastructure.Auth;
using Reflow.Infrastructure.Files;
using Reflow.Infrastructure.Results;
using Reflow.Modules.AiAssistant.Options;

namespace Reflow.Modules.AiAssistant.Features.GenerateWorkflow;

public sealed record GenerateWorkflowRequest(string Prompt, string? ApiKey = null);

public sealed record GeneratedNodeDto(
    string NodeId, string NodeType, string? Label, JsonElement Config);

public sealed record GeneratedEdgeDto(string SourceNodeId, string TargetNodeId);

public sealed record GenerateWorkflowResponse(
    string Name,
    string Explanation,
    IReadOnlyList<GeneratedNodeDto> Nodes,
    IReadOnlyList<GeneratedEdgeDto> Edges,
    IReadOnlyList<string> Warnings,
    int RepairAttempts);

public sealed class GenerateWorkflowHandler(
    WorkflowGenerator generator,
    ISourceFileCatalog files,
    IOptions<AiOptions> options)
{
    public async Task<Result<GenerateWorkflowResponse>> Handle(
        GenerateWorkflowRequest request, Guid ownerId, CancellationToken ct)
    {
        var prompt = request.Prompt?.Trim() ?? string.Empty;
        if (prompt.Length < 3)
            return Result<GenerateWorkflowResponse>.Failure(
                "Describe the workflow you want (at least 3 characters)", 400);
        if (prompt.Length > options.Value.MaxPromptChars)
            return Result<GenerateWorkflowResponse>.Failure(
                $"Prompt is too long (max {options.Value.MaxPromptChars} characters)", 400);

        // Only the caller's own files, metadata only: the model can reference a
        // real id without ever seeing content or another user's data.
        var availableFiles = await files.ListAsync(ownerId, ct);

        var result = await generator.GenerateAsync(prompt, availableFiles, request.ApiKey, ct);
        if (!result.IsSuccess)
            return Result<GenerateWorkflowResponse>.Failure(result.Error!, result.StatusCode);

        var draft = result.Value!.Draft;
        return Result<GenerateWorkflowResponse>.Success(new GenerateWorkflowResponse(
            draft.Name,
            draft.Explanation,
            draft.Nodes
                .Select(n => new GeneratedNodeDto(n.NodeId, n.NodeType, n.Label, n.Config))
                .ToList(),
            draft.Edges
                .Select(e => new GeneratedEdgeDto(e.SourceNodeId, e.TargetNodeId))
                .ToList(),
            draft.Warnings,
            result.Value.RepairAttempts));
    }
}

public static class GenerateWorkflowEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/v1/assistant/workflows/generate", async (
            GenerateWorkflowHandler handler,
            GenerateWorkflowRequest request,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (!http.User.TryGetUserId(out var ownerId))
                return Results.Json(new { error = "Unauthorized" }, statusCode: 401);

            var result = await handler.Handle(request, ownerId, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.Json(new { error = result.Error }, statusCode: result.StatusCode);
        })
        .RequireAuthorization()
        .RequireRateLimiting("ai")
        .WithName("GenerateWorkflow")
        .Produces<GenerateWorkflowResponse>(200)
        .Produces(400)
        .Produces(422)
        .Produces(503);
    }
}
