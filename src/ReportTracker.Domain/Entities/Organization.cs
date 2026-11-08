using ReportTracker.Domain.Common;

namespace ReportTracker.Domain.Entities;

public sealed class Division : Entity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<Team> Teams { get; set; } = [];
}

public sealed class Team : Entity
{
    public required string Name { get; set; }
    public Guid DivisionId { get; set; }
    public Division Division { get; set; } = null!;
}

public sealed class AppUser : Entity
{
    public required string EmployeeId { get; set; }
    public required string DisplayName { get; set; }
    public required string Email { get; set; }
    public string? MobileNumber { get; set; }
    public Guid DivisionId { get; set; }
    public Division Division { get; set; } = null!;
    public bool IsActive { get; set; } = true;
}

public sealed class Delegation : Entity
{
    public Guid OwnerUserId { get; set; }
    public Guid DelegateUserId { get; set; }
    public Guid GrantedByUserId { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime EffectiveToUtc { get; set; }
    public string? Scope { get; set; }
}
