using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Reflow.Infrastructure.Results;
using Reflow.Modules.PipelineExecution.Domain;
using Reflow.Modules.PipelineExecution.Features.StartRun;
using Reflow.Modules.Triggers.Domain;
using Reflow.Modules.Triggers.Persistence;
using Reflow.Modules.Triggers.Services;

namespace Reflow.Modules.Triggers.Features.Webhooks;

public class WebhookHandler(TriggersDbContext db, IPipelineAccessChecker pipelines)
{
    public async Task<Result<object>> CreateAsync(
        Guid pipelineId, string name, Guid ownerId, CancellationToken ct)
    {
        if (!await pipelines.OwnsAsync(pipelineId, ownerId, ct))
            return Result<object>.Failure("Pipeline not found", 404);
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
            return Result<object>.Failure("Name must be 1-200 characters", 400);

        var token = WebhookToken.Generate();
        db.Triggers.Add(new Trigger
        {
            Id = Guid.NewGuid(),
            PipelineId = pipelineId,
            OwnerId = ownerId,
            Kind = TriggerKind.Webhook,
            Name = name.Trim(),
            IsEnabled = true,
            SecretTokenHash = WebhookToken.Hash(token),
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
        return Result<object>.Success(new { token }, 201);
    }

    public async Task<Result<object>> RegenerateAsync(
        Guid pipelineId, Guid triggerId, Guid ownerId, CancellationToken ct)
    {
        var trigger = await db.Triggers.FirstOrDefaultAsync(
            t => t.Id == triggerId && t.PipelineId == pipelineId
                && t.OwnerId == ownerId && t.Kind == TriggerKind.Webhook, ct);
        if (trigger is null)
            return Result<object>.Failure("Webhook not found", 404);

        var token = WebhookToken.Generate();
        trigger.SecretTokenHash = WebhookToken.Hash(token);
        await db.SaveChangesAsync(ct);
        return Result<object>.Success(new { token }, 200);
    }
}

public class ReceiveWebhookHandler(
    TriggersDbContext db,
    IPipelineRunStarter starter,
    IRunMonitor monitor)
{
    public const int MaxQueuedRuns = 5;
    public const int MaxBodyBytes = 600 * 1024;

    public async Task<IResult> HandleAsync(
        string token, HttpRequest request, CancellationToken ct)
    {
        var trigger = await db.Triggers.FirstOrDefaultAsync(
            t => t.Kind == TriggerKind.Webhook && t.SecretTokenHash == WebhookToken.Hash(token), ct);
        if (trigger is null || !trigger.IsEnabled)
            return Results.Json(new { error = "Not found" }, statusCode: 404);

        string? body = null;
        if (request.ContentLength > MaxBodyBytes)
            return Results.Json(new { error = "Payload too large" }, statusCode: 413);
        using (var reader = new StreamReader(request.Body, Encoding.UTF8))
        {
            body = await reader.ReadToEndAsync(ct);
            if (body.Length > MaxBodyBytes)
                return Results.Json(new { error = "Payload too large" }, statusCode: 413);
        }
        if (string.IsNullOrWhiteSpace(body))
            body = null;
        else
        {
            try { JsonDocument.Parse(body); }
            catch (JsonException) { return Results.Json(new { error = "Body must be JSON" }, statusCode: 400); }
        }

        string? externalId = request.Headers["X-Event-Id"].FirstOrDefault();
        if (externalId is null && body is not null)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("eventId", out var e)
                    && e.ValueKind == JsonValueKind.String)
                    externalId = e.GetString();
            }
            catch (JsonException) { }
        }
        if (externalId?.Length > 200)
            return Results.Json(new { error = "Event id too long" }, statusCode: 400);

        if (externalId is not null)
        {
            var existing = await db.WebhookEvents.FirstOrDefaultAsync(
                e => e.TriggerId == trigger.Id && e.ExternalEventId == externalId, ct);
            if (existing?.PipelineRunId is not null)
            {
                existing.ProcessingStatus = WebhookProcessingStatus.Duplicate;
                await db.SaveChangesAsync(ct);
                return Results.Json(new { id = existing.PipelineRunId, duplicate = true }, statusCode: 200);
            }
        }

        var active = await monitor.CountActiveRunsAsync(trigger.PipelineId, ct);
        if (active >= MaxQueuedRuns)
            return Results.Json(new { error = "Too many active runs" }, statusCode: 429);

        var evt = new WebhookEvent
        {
            Id = Guid.NewGuid(),
            TriggerId = trigger.Id,
            ExternalEventId = externalId,
            PayloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body ?? string.Empty))),
            ProcessingStatus = WebhookProcessingStatus.Received,
            ReceivedAt = DateTime.UtcNow,
        };
        db.WebhookEvents.Add(evt);
        await db.SaveChangesAsync(ct);

        var started = await starter.StartRunAsync(
            trigger.PipelineId, trigger.OwnerId, "webhook", body, ct);
        if (!started.IsSuccess)
        {
            evt.ProcessingStatus = WebhookProcessingStatus.Rejected;
            await db.SaveChangesAsync(ct);
            return Results.Json(new { error = started.Error }, statusCode: started.StatusCode);
        }

        evt.ProcessingStatus = WebhookProcessingStatus.Started;
        evt.PipelineRunId = started.Value;
        await db.SaveChangesAsync(ct);
        return Results.Json(new { id = started.Value }, statusCode: 202);
    }
}

public static class WebhookEndpoints
{
    private static bool TryOwner(HttpContext http, out Guid ownerId) =>
        Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out ownerId);

    public static void Map(WebApplication app)
    {
        app.MapPost("/api/v1/pipelines/{id:guid}/triggers/webhooks", async (
            Guid id,
            WebhookHandler handler,
            CreateWebhookRequest request,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (!TryOwner(http, out var ownerId))
                return Results.Json(new { error = "Unauthorized" }, statusCode: 401);
            var result = await handler.CreateAsync(id, request.Name ?? "Webhook", ownerId, ct);
            return result.IsSuccess
                ? Results.Json(result.Value, statusCode: result.StatusCode)
                : Results.Json(new { error = result.Error }, statusCode: result.StatusCode);
        }).RequireAuthorization().WithName("CreateWebhook");

        app.MapPost("/api/v1/pipelines/{pid:guid}/triggers/webhooks/{tid:guid}/regenerate", async (
            Guid pid,
            Guid tid,
            WebhookHandler handler,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (!TryOwner(http, out var ownerId))
                return Results.Json(new { error = "Unauthorized" }, statusCode: 401);
            var result = await handler.RegenerateAsync(pid, tid, ownerId, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.Json(new { error = result.Error }, statusCode: result.StatusCode);
        }).RequireAuthorization().WithName("RegenerateWebhook");

        app.MapPost("/api/v1/hooks/{token}", (
            string token,
            ReceiveWebhookHandler handler,
            HttpRequest request,
            CancellationToken ct) =>
            handler.HandleAsync(token, request, ct))
            .AllowAnonymous()
            .RequireRateLimiting("webhook")
            .WithName("ReceiveWebhook");
    }

    public sealed record CreateWebhookRequest(string? Name);
}
