namespace ReportTracker.Application.Abstractions;

public interface ICurrentUser
{
    Guid UserId { get; }
}
