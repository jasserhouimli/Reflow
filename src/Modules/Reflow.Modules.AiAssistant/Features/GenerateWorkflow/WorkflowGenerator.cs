using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reflow.Infrastructure.Files;
using Reflow.Infrastructure.Results;
using Reflow.Modules.AiAssistant.Abstractions;
using Reflow.Modules.AiAssistant.Options;

namespace Reflow.Modules.AiAssistant.Features.GenerateWorkflow;

public sealed record WorkflowGeneration(WorkflowDraft Draft, int RepairAttempts);

/// <summary>
/// Produces a validated draft. The model proposes; this class disposes: parse,
/// validate, repair at most N times, then fail with the reasons.
/// </summary>
public sealed class WorkflowGenerator(
    ILanguageModel model,
    PromptBuilder promptBuilder,
    WorkflowDraftValidator validator,
    IOptions<AiOptions> options,
    ILogger<WorkflowGenerator> logger)
{
    public async Task<Result<WorkflowGeneration>> GenerateAsync(
        string request,
        IReadOnlyList<SourceFileRef> files,
        CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.IsConfigured)
            return Result<WorkflowGeneration>.Failure("AI features are not configured", 503);

        var correlationId = Guid.NewGuid().ToString("N")[..12];
        var systemPrompt = promptBuilder.BuildSystemPrompt(files);
        var userPrompt = PromptBuilder.BuildUserPrompt(request);
        var stopwatch = Stopwatch.StartNew();
        var errors = new List<string> { "the model did not return a usable draft" };

        for (var attempt = 0; attempt <= settings.MaxRepairs; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            string raw;
            try
            {
                raw = await model.CompleteJsonAsync(systemPrompt, userPrompt, ct);
            }
            catch (LanguageModelException ex) when (ex.StatusCode == 429)
            {
                logger.LogWarning(
                    "AI generation {CorrelationId} rate limited by the provider after {ElapsedMs}ms",
                    correlationId, stopwatch.ElapsedMilliseconds);
                return Result<WorkflowGeneration>.Failure(
                    "The AI provider is rate limited. Try again in a moment.", 429);
            }
            catch (LanguageModelException ex)
            {
                logger.LogWarning(
                    "AI generation {CorrelationId} failed: provider status {StatusCode} after {ElapsedMs}ms",
                    correlationId, ex.StatusCode, stopwatch.ElapsedMilliseconds);
                return Result<WorkflowGeneration>.Failure("The AI provider is unavailable.", 502);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning(
                    "AI generation {CorrelationId} timed out after {ElapsedMs}ms",
                    correlationId, stopwatch.ElapsedMilliseconds);
                return Result<WorkflowGeneration>.Failure("The AI provider timed out.", 504);
            }
            catch (HttpRequestException)
            {
                logger.LogWarning(
                    "AI generation {CorrelationId} could not reach the provider after {ElapsedMs}ms",
                    correlationId, stopwatch.ElapsedMilliseconds);
                return Result<WorkflowGeneration>.Failure("The AI provider is unreachable.", 502);
            }

            var (draft, parseError) = WorkflowDraftParser.Parse(raw);
            if (draft is null)
            {
                errors = [parseError!];
            }
            else
            {
                var validationErrors = validator.Validate(draft, settings.MaxDraftNodes);
                if (validationErrors.Count == 0)
                {
                    logger.LogInformation(
                        "AI generation {CorrelationId} succeeded: {NodeCount} nodes, {Attempts} attempt(s), {ElapsedMs}ms",
                        correlationId, draft.Nodes.Count, attempt + 1, stopwatch.ElapsedMilliseconds);
                    return Result<WorkflowGeneration>.Success(new WorkflowGeneration(draft, attempt));
                }
                errors = validationErrors.ToList();
            }

            userPrompt = PromptBuilder.BuildRepairPrompt(
                request, errors, Truncate(raw, 4000));
        }

        logger.LogWarning(
            "AI generation {CorrelationId} produced no valid draft after {Attempts} attempt(s): {ErrorCount} error(s)",
            correlationId, settings.MaxRepairs + 1, errors.Count);

        var summary = string.Join("; ", errors.Take(5));
        return Result<WorkflowGeneration>.Failure(
            $"The AI could not produce a valid workflow: {summary}", 422);
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";
}
