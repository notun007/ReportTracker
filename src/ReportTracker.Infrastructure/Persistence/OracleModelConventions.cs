using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ReportTracker.Domain.Entities;

namespace ReportTracker.Infrastructure.Persistence;

internal static partial class OracleModelConventions
{
    private const int OracleIdentifierLimit = 128;

    private static readonly IReadOnlyDictionary<Type, string> TableNames = new Dictionary<Type, string>
    {
        [typeof(Division)] = "rt_divisions",
        [typeof(Team)] = "rt_teams",
        [typeof(AppUser)] = "rt_app_users",
        [typeof(LocalCredential)] = "rt_local_credentials",
        [typeof(Delegation)] = "rt_delegations",
        [typeof(ReportDefinition)] = "rt_report_definitions",
        [typeof(ReportType)] = "rt_report_types",
        [typeof(SubmissionRecipient)] = "rt_submission_recipients",
        [typeof(SubmissionRecipientDepartment)] = "rt_submission_recipient_departments",
        [typeof(ReportSubmissionDepartment)] = "rt_report_submission_departments",
        [typeof(DefinitionVersion)] = "rt_definition_versions",
        [typeof(ComponentDefinition)] = "rt_component_definitions",
        [typeof(ReportCycle)] = "rt_report_cycles",
        [typeof(TrackingObligation)] = "rt_tracking_obligations",
        [typeof(ChannelRequirement)] = "rt_channel_requirements",
        [typeof(SendingEvent)] = "rt_sending_events",
        [typeof(ReceiptConfirmation)] = "rt_receipt_confirmations",
        [typeof(EvidenceDocument)] = "rt_evidence_documents",
        [typeof(EvidenceDecision)] = "rt_evidence_decisions",
        [typeof(CorrectionCase)] = "rt_correction_cases",
        [typeof(ExtensionRequest)] = "rt_extension_requests",
        [typeof(AuditEvent)] = "rt_audit_events",
        [typeof(AppRole)] = "rt_app_roles",
        [typeof(Permission)] = "rt_permissions",
        [typeof(UserRole)] = "rt_user_roles",
        [typeof(RolePermission)] = "rt_role_permissions",
        [typeof(ResponsibilityAssignment)] = "rt_responsibility_assignments",
        [typeof(ScheduleRule)] = "rt_schedule_rules",
        [typeof(ReportingFrequency)] = "rt_reporting_frequencies",
        [typeof(SchedulePreset)] = "rt_schedule_presets",
        [typeof(StatusAmendment)] = "rt_status_amendments",
        [typeof(ExtensionDecision)] = "rt_extension_decisions",
        [typeof(ReminderPolicy)] = "rt_reminder_policies",
        [typeof(NotificationJob)] = "rt_notification_jobs",
        [typeof(NotificationDelivery)] = "rt_notification_deliveries",
        [typeof(CalendarLink)] = "rt_calendar_links",
        [typeof(HolidayCalendar)] = "rt_holiday_calendars",
        [typeof(HolidayDate)] = "rt_holiday_dates"
    };

    private static readonly ValueConverter<DateOnly, DateTime> DateOnlyConverter =
        new(value => value.ToDateTime(TimeOnly.MinValue), value => DateOnly.FromDateTime(value));

    private static readonly ValueConverter<DateOnly?, DateTime?> NullableDateOnlyConverter =
        new(value => value.HasValue ? value.Value.ToDateTime(TimeOnly.MinValue) : null,
            value => value.HasValue ? DateOnly.FromDateTime(value.Value) : null);

    private static readonly ValueConverter<TimeOnly, TimeSpan> TimeOnlyConverter =
        new(value => value.ToTimeSpan(), value => TimeOnly.FromTimeSpan(value));

    private static readonly ValueConverter<TimeOnly?, TimeSpan?> NullableTimeOnlyConverter =
        new(value => value.HasValue ? value.Value.ToTimeSpan() : null,
            value => value.HasValue ? TimeOnly.FromTimeSpan(value.Value) : null);

    public static void ApplyOracleConventions(this ModelBuilder modelBuilder)
    {
        ConfigureIndexedStrings(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!TableNames.TryGetValue(entityType.ClrType, out var tableName))
                throw new InvalidOperationException($"No Oracle table name is configured for {entityType.ClrType.Name}.");

            entityType.SetTableName(tableName);
            entityType.SetSchema(null);

            foreach (var property in entityType.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
                ConfigureOracleType(property);
            }
        }

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName()!;

            foreach (var key in entityType.GetKeys())
            {
                var prefix = key.IsPrimaryKey() ? "pk" : "uq";
                key.SetName(Identifier(prefix, tableName, ColumnList(key.Properties)));
            }

            foreach (var index in entityType.GetIndexes())
            {
                var prefix = index.IsUnique ? "uq" : "ix";
                index.SetDatabaseName(Identifier(prefix, tableName, ColumnList(index.Properties)));
            }

            foreach (var foreignKey in entityType.GetForeignKeys())
            {
                foreignKey.SetConstraintName(Identifier(
                    "fk",
                    tableName,
                    foreignKey.PrincipalEntityType.GetTableName()!,
                    ColumnList(foreignKey.Properties)));
            }
        }
    }

    private static void ConfigureIndexedStrings(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Division>().Property(x => x.Code).HasMaxLength(40);
        modelBuilder.Entity<AppUser>().Property(x => x.EmployeeId).HasMaxLength(50);
        modelBuilder.Entity<AppUser>().Property(x => x.Email).HasMaxLength(320);
        modelBuilder.Entity<AppRole>().Property(x => x.Name).HasMaxLength(100);
        modelBuilder.Entity<Permission>().Property(x => x.Code).HasMaxLength(100);
        modelBuilder.Entity<ChannelRequirement>().Property(x => x.Channel).HasMaxLength(50);
        modelBuilder.Entity<NotificationJob>().Property(x => x.LogicalKey).HasMaxLength(500);
        modelBuilder.Entity<CalendarLink>().Property(x => x.Provider).HasMaxLength(50);
        modelBuilder.Entity<HolidayCalendar>().Property(x => x.Name).HasMaxLength(200);
    }

    private static void ConfigureOracleType(IMutableProperty property)
    {
        var type = property.ClrType;

        if (type == typeof(Guid) || type == typeof(Guid?))
            property.SetColumnType("RAW(16)");
        else if (type == typeof(bool) || type == typeof(bool?))
            property.SetColumnType("NUMBER(1)");
        else if (type == typeof(DateTime) || type == typeof(DateTime?))
            property.SetColumnType("TIMESTAMP(6)");
        else if (type == typeof(DateOnly))
        {
            property.SetValueConverter(DateOnlyConverter);
            property.SetColumnType("DATE");
        }
        else if (type == typeof(DateOnly?))
        {
            property.SetValueConverter(NullableDateOnlyConverter);
            property.SetColumnType("DATE");
        }
        else if (type == typeof(TimeOnly))
        {
            property.SetValueConverter(TimeOnlyConverter);
            property.SetColumnType("INTERVAL DAY(0) TO SECOND(6)");
        }
        else if (type == typeof(TimeOnly?))
        {
            property.SetValueConverter(NullableTimeOnlyConverter);
            property.SetColumnType("INTERVAL DAY(0) TO SECOND(6)");
        }
        else if (type == typeof(byte[]) && property.Name == "RowVersion")
            property.SetColumnType("RAW(8)");
        else if (type == typeof(string))
        {
            if (property.Name.EndsWith("Json", StringComparison.Ordinal))
                property.SetColumnType("NCLOB");
            else if (property.GetColumnType() is null && property.GetMaxLength() is null)
                property.SetMaxLength(2000);
        }
    }

    private static string ColumnList(IEnumerable<IMutableProperty> properties) =>
        string.Join('_', properties.Select(property => ToSnakeCase(property.Name)));

    private static string Identifier(params string[] parts)
    {
        var identifier = string.Join('_', parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        if (identifier.Length <= OracleIdentifierLimit)
            return identifier;

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identifier)))[..10].ToLowerInvariant();
        return $"{identifier[..(OracleIdentifierLimit - hash.Length - 1)]}_{hash}";
    }

    private static string ToSnakeCase(string value) =>
        SnakeCaseBoundary().Replace(value, "$1_$2").ToLowerInvariant();

    [GeneratedRegex("([a-z0-9])([A-Z])")]
    private static partial Regex SnakeCaseBoundary();
}
