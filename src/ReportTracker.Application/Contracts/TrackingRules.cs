namespace ReportTracker.Application.Contracts;

public sealed class TrackingValidationException(string message) : Exception(message);
public sealed class TrackingConflictException(string message) : Exception(message);

public static class TrackingRules
{
    public static readonly TimeZoneInfo Dhaka = TimeZoneInfo.FindSystemTimeZoneById("Asia/Dhaka");

    public static string Timeliness(DateTime dueUtc, DateTime? actualSentUtc, DateTime nowUtc)
    {
        if (actualSentUtc.HasValue) return actualSentUtc <= dueUtc ? "Sent On Time" : "Sent Late";
        if (dueUtc < nowUtc) return "Overdue";
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(dueUtc, DateTimeKind.Utc), Dhaka).Date ==
               TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), Dhaka).Date
            ? "Due Today" : "Upcoming";
    }

    public static void ValidateSending(RecordSendingCommand command, DateTime nowUtc)
    {
        if (command.ActualSentAtUtc.Kind != DateTimeKind.Utc || command.ActualSentAtUtc < new DateTime(2000, 1, 1) || command.ActualSentAtUtc > nowUtc.AddMinutes(5))
            throw new TrackingValidationException("Enter a valid actual sending time in UTC that is not in the future.");
        if (string.IsNullOrWhiteSpace(command.Recipient) || command.Recipient.Length > 250)
            throw new TrackingValidationException("Recipient is required and must be at most 250 characters.");
        if (command.Channel is not ("Portal" or "Email" or "Physical letter" or "Other"))
            throw new TrackingValidationException("Choose a supported sending channel.");
        if (command.Reference?.Length > 250 || command.TrackingRemarks?.Length > 1000)
            throw new TrackingValidationException("Reference must be at most 250 characters and tracking remarks at most 1000.");
        if (command.IsPartial && (command.ScopeNames is not { Count: > 0 } || command.ScopeNames.Any(string.IsNullOrWhiteSpace)))
            throw new TrackingValidationException("Name the components included in this partial sending record.");
        if (command.ScopeNames?.Count > 50 || command.ScopeNames?.Any(x => x is null || x.Length > 100) == true)
            throw new TrackingValidationException("Provide at most 50 component names, each at most 100 characters.");
        if (string.IsNullOrWhiteSpace(command.RowVersion))
            throw new TrackingValidationException("Refresh the obligation before saving.");
        try { if (Convert.FromBase64String(command.RowVersion).Length != 8) throw new FormatException(); }
        catch (FormatException) { throw new TrackingValidationException("Invalid record version. Refresh and try again."); }
    }
}
