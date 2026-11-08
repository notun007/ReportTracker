using ReportTracker.Application.Contracts;

namespace ReportTracker.Application.Abstractions;

public interface ICycleGenerationService
{
    Task<GenerationResult> GenerateAsync(DateOnly throughLocalDate, CancellationToken cancellationToken);
}
