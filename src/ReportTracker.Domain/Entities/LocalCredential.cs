using ReportTracker.Domain.Common;

namespace ReportTracker.Domain.Entities;

public sealed class LocalCredential : Entity
{
    public Guid UserId { get; set; }
    public required string PasswordHash { get; set; }
    public int FailedAttempts { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
}
