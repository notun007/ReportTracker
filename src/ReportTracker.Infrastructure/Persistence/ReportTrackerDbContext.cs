using Microsoft.EntityFrameworkCore;
using ReportTracker.Domain.Common;
using ReportTracker.Domain.Entities;

namespace ReportTracker.Infrastructure.Persistence;

public sealed class ReportTrackerDbContext(DbContextOptions<ReportTrackerDbContext> options) : DbContext(options)
{
    public DbSet<Division> Divisions => Set<Division>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<LocalCredential> LocalCredentials => Set<LocalCredential>();
    public DbSet<Delegation> Delegations => Set<Delegation>();
    public DbSet<ReportDefinition> ReportDefinitions => Set<ReportDefinition>();
    public DbSet<DefinitionVersion> DefinitionVersions => Set<DefinitionVersion>();
    public DbSet<ComponentDefinition> ComponentDefinitions => Set<ComponentDefinition>();
    public DbSet<ReportCycle> ReportCycles => Set<ReportCycle>();
    public DbSet<TrackingObligation> TrackingObligations => Set<TrackingObligation>();
    public DbSet<ChannelRequirement> ChannelRequirements => Set<ChannelRequirement>();
    public DbSet<SendingEvent> SendingEvents => Set<SendingEvent>();
    public DbSet<ReceiptConfirmation> ReceiptConfirmations => Set<ReceiptConfirmation>();
    public DbSet<EvidenceDocument> EvidenceDocuments => Set<EvidenceDocument>();
    public DbSet<EvidenceDecision> EvidenceDecisions => Set<EvidenceDecision>();
    public DbSet<CorrectionCase> CorrectionCases => Set<CorrectionCase>();
    public DbSet<ExtensionRequest> ExtensionRequests => Set<ExtensionRequest>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<AppRole> Roles => Set<AppRole>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<ResponsibilityAssignment> ResponsibilityAssignments => Set<ResponsibilityAssignment>();
    public DbSet<ScheduleRule> ScheduleRules => Set<ScheduleRule>();
    public DbSet<StatusAmendment> StatusAmendments => Set<StatusAmendment>();
    public DbSet<ExtensionDecision> ExtensionDecisions => Set<ExtensionDecision>();
    public DbSet<ReminderPolicy> ReminderPolicies => Set<ReminderPolicy>();
    public DbSet<NotificationJob> NotificationJobs => Set<NotificationJob>();
    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();
    public DbSet<CalendarLink> CalendarLinks => Set<CalendarLink>();
    public DbSet<HolidayCalendar> HolidayCalendars => Set<HolidayCalendar>();
    public DbSet<HolidayDate> HolidayDates => Set<HolidayDate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("tracker");

        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(x => typeof(Entity).IsAssignableFrom(x.ClrType)))
        {
            modelBuilder.Entity(entityType.ClrType).Property(nameof(Entity.RowVersion)).IsRowVersion();
        }

        modelBuilder.Entity<Division>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<AppUser>().HasIndex(x => x.EmployeeId).IsUnique();
        modelBuilder.Entity<AppUser>().HasIndex(x => x.Email).IsUnique();
        modelBuilder.Entity<LocalCredential>().HasIndex(x => x.UserId).IsUnique();
        modelBuilder.Entity<LocalCredential>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ReportDefinition>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<DefinitionVersion>().HasIndex(x => new { x.ReportDefinitionId, x.VersionNumber }).IsUnique();
        modelBuilder.Entity<ReportCycle>().HasIndex(x => new { x.ReportDefinitionId, x.PeriodKey }).IsUnique();
        modelBuilder.Entity<ChannelRequirement>().HasIndex(x => new { x.TrackingObligationId, x.Channel }).IsUnique();
        modelBuilder.Entity<AppRole>().HasIndex(x => x.Name).IsUnique();
        modelBuilder.Entity<Permission>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<UserRole>().HasIndex(x => new { x.UserId, x.RoleId, x.DivisionId, x.ReportDefinitionId }).IsUnique();
        modelBuilder.Entity<RolePermission>().HasIndex(x => new { x.RoleId, x.PermissionId }).IsUnique();
        modelBuilder.Entity<NotificationJob>().HasIndex(x => x.LogicalKey).IsUnique();
        modelBuilder.Entity<CalendarLink>().HasIndex(x => new { x.TrackingObligationId, x.UserId, x.Provider }).IsUnique();
        modelBuilder.Entity<HolidayCalendar>().HasIndex(x => new { x.Name, x.Year }).IsUnique();
        modelBuilder.Entity<HolidayDate>().HasIndex(x => new { x.HolidayCalendarId, x.Date }).IsUnique();

        modelBuilder.Entity<ReportDefinition>()
            .HasOne<Division>().WithMany().HasForeignKey(x => x.OwnerDivisionId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<TrackingObligation>()
            .HasOne<Division>().WithMany().HasForeignKey(x => x.AccountableDivisionId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<TrackingObligation>()
            .HasOne(x => x.ReportCycle).WithMany(x => x.Obligations).HasForeignKey(x => x.ReportCycleId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SendingEvent>()
            .HasOne(x => x.TrackingObligation).WithMany(x => x.SendingEvents).HasForeignKey(x => x.TrackingObligationId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DefinitionVersion>().Property(x => x.TimeZoneId).HasMaxLength(64);
        modelBuilder.Entity<ReportDefinition>().Property(x => x.Code).HasMaxLength(40);
        modelBuilder.Entity<ReportCycle>().Property(x => x.PeriodKey).HasMaxLength(80);
        modelBuilder.Entity<TrackingObligation>().Property(x => x.Name).HasMaxLength(250);
        modelBuilder.Entity<SendingEvent>().Property(x => x.TrackingRemarks).HasMaxLength(1000);
        modelBuilder.Entity<CorrectionCase>().Property(x => x.Explanation).HasMaxLength(1000);
        modelBuilder.Entity<AuditEvent>().Property(x => x.DataJson).HasColumnType("nvarchar(max)");
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<Entity>().Where(x => x.State == EntityState.Modified))
        {
            entry.Entity.UpdatedAtUtc = DateTime.UtcNow;
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
