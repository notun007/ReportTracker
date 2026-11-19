using ReportTracker.Application.Contracts;

namespace ReportTracker.Application.Abstractions;

public interface ICycleGenerationService
{
    Task<GenerationResult> GenerateAsync(DateOnly throughLocalDate, CancellationToken cancellationToken);
    Task<YearlyCyclePreview> PreviewYearAsync(int year, Guid? reportId, CancellationToken cancellationToken);
    Task<YearlyCycleGenerationResult> GenerateYearAsync(int year, Guid? reportId, IReadOnlyCollection<string>? periodKeys,
        CancellationToken cancellationToken);
}
