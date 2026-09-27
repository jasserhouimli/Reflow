using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Reflow.Infrastructure.Results;
using Reflow.Modules.Triggers.Domain;
using Reflow.Modules.Triggers.Persistence;
using Reflow.Modules.Triggers.Services;

namespace Reflow.Modules.Triggers.Features.Schedules;

public record CreateScheduleRequest(
    string Name, string Cron, string Timezone, int Overlap, bool IsEnabled = true);
public record UpdateScheduleRequest(
    string? Name, string? Cron, string? Timezone, int? Overlap, bool? IsEnabled);

public static class ScheduleValidator
{
    public static IReadOnlyList<string> Validate(string? cron, string? timezone, int? overlap)
    {
        var errors = new List<string>();
        if (cron is not null && !CronSchedule.TryParse(cron, out var cronError))
            errors.Add(cronError!);
        if (timezone is not null && !CronSchedule.TryTimezone(timezone, out _))
            errors.Add($"Unknown timezone '{timezone}'");
        if (overlap is not null && overlap is not 0 and not 1)
            errors.Add("Overlap must be 0 (skip) or 1 (queue)");
        return errors;
    }
}

public class ScheduleHandler(TriggersDbContext db, IPipelineAccessChecker pipelines)
{
    public async Task<Result<object>> CreateAsync(
        Guid pipelineId, CreateScheduleRequest request, Guid ownerId, CancellationToken ct)
    {
        if (!await pipelines.OwnsAsync(pipelineId, ownerId, ct))
            return Result<object>.Failure("Pipeline not found", 404);
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 200)
            return Result<object>.Failure("Name must be 1-200 characters", 400);
        var errors = ScheduleValidator.Validate(request.Cron, request.Timezone, request.Overlap);
        if (errors.Count > 0)
            return Result<object>.Failure(string.Join("; ", errors), 400);

        var trigger = new Trigger
        {
            Id = Guid.NewGuid(),
            PipelineId = pipelineId,
            OwnerId = ownerId,
            Kind = TriggerKind.Schedule,
            Name = request.Name.Trim(),
            IsEnabled = request.IsEnabled,
            Cron = request.Cron,
            Timezone = request.Timezone,
            Overlap = (OverlapPolicy)request.Overlap,
            NextRunAt = request.IsEnabled
                ? CronSchedule.NextOccurrence(request.Cron, request.Timezone, DateTime.UtcNow)
                : null,
            CreatedAt = DateTime.UtcNow,
        };
        db.Triggers.Add(trigger);
        await db.SaveChangesAsync(ct);
        return Result<object>.Success(new { id = trigger.Id }, 201);
    }

    public async Task<Result<object>> UpdateAsync(
        Guid pipelineId, Guid triggerId, UpdateScheduleRequest request, Guid ownerId, CancellationToken ct)
    {
        var trigger = await db.Triggers.FirstOrDefaultAsync(
            t => t.Id == triggerId && t.PipelineId == pipelineId
                && t.OwnerId == ownerId && t.Kind == TriggerKind.Schedule, ct);
        if (trigger is null)
            return Result<object>.Failure("Schedule not found", 404);

        var errors = ScheduleValidator.Validate(request.Cron, request.Timezone, request.Overlap);
        if (errors.Count > 0)
            return Result<object>.Failure(string.Join("; ", errors), 400);

        if (request.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 200)
                return Result<object>.Failure("Name must be 1-200 characters", 400);
            trigger.Name = request.Name.Trim();
        }
        if (request.Cron is not null)
            trigger.Cron = request.Cron;
        if (request.Timezone is not null)
            trigger.Timezone = request.Timezone;
        if (request.Overlap is not null)
            trigger.Overlap = (OverlapPolicy)request.Overlap.Value;
        if (request.IsEnabled is not null)
            trigger.IsEnabled = request.IsEnabled.Value;
        trigger.NextRunAt = trigger.IsEnabled && trigger.Cron is not null && trigger.Timezone is not null
            ? CronSchedule.NextOccurrence(trigger.Cron, trigger.Timezone, DateTime.UtcNow)
            : null;
        await db.SaveChangesAsync(ct);
        return Result<object>.Success(new { id = trigger.Id }, 200);
    }
}

public static class ScheduleEndpoints
{
    private static bool TryOwner(HttpContext http, out Guid ownerId) =>
        Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out ownerId);

    public static void Map(WebApplication app)
    {
        app.MapGet("/api/v1/pipelines/{id:guid}/triggers", async (
            Guid id,
            TriggersDbContext db,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (!TryOwner(http, out var ownerId))
                return Results.Json(new { error = "Unauthorized" }, statusCode: 401);
            var triggers = await db.Triggers.AsNoTracking()
                .Where(t => t.PipelineId == id && t.OwnerId == ownerId)
                .OrderBy(t => t.CreatedAt)
                .Select(t => new
                {
                    t.Id,
                    kind = (int)t.Kind,
                    t.Name,
                    t.IsEnabled,
                    t.Cron,
                    t.Timezone,
                    overlap = (int)t.Overlap,
                    t.NextRunAt,
                    t.LastFiredAt,
                })
                .ToListAsync(ct);
            return Results.Ok(triggers);
        }).RequireAuthorization().WithName("ListTriggers");

        app.MapPost("/api/v1/pipelines/{id:guid}/triggers/schedules", async (
            Guid id,
            ScheduleHandler handler,
            CreateScheduleRequest request,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (!TryOwner(http, out var ownerId))
                return Results.Json(new { error = "Unauthorized" }, statusCode: 401);
            var result = await handler.CreateAsync(id, request, ownerId, ct);
            return result.IsSuccess
                ? Results.Json(result.Value, statusCode: result.StatusCode)
                : Results.Json(new { error = result.Error }, statusCode: result.StatusCode);
        }).RequireAuthorization().WithName("CreateSchedule");

        app.MapPut("/api/v1/pipelines/{pid:guid}/triggers/schedules/{tid:guid}", async (
            Guid pid,
            Guid tid,
            ScheduleHandler handler,
            UpdateScheduleRequest request,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (!TryOwner(http, out var ownerId))
                return Results.Json(new { error = "Unauthorized" }, statusCode: 401);
            var result = await handler.UpdateAsync(pid, tid, request, ownerId, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.Json(new { error = result.Error }, statusCode: result.StatusCode);
        }).RequireAuthorization().WithName("UpdateSchedule");

        app.MapDelete("/api/v1/pipelines/{pid:guid}/triggers/{tid:guid}", async (
            Guid pid,
            Guid tid,
            TriggersDbContext db,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (!TryOwner(http, out var ownerId))
                return Results.Json(new { error = "Unauthorized" }, statusCode: 401);
            var trigger = await db.Triggers.FirstOrDefaultAsync(
                t => t.Id == tid && t.PipelineId == pid && t.OwnerId == ownerId, ct);
            if (trigger is null)
                return Results.Json(new { error = "Trigger not found" }, statusCode: 404);
            db.Triggers.Remove(trigger);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization().WithName("DeleteTrigger");
    }
}
