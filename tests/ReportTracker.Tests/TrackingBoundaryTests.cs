using ReportTracker.Domain.Entities;

namespace ReportTracker.Tests;

public sealed class TrackingBoundaryTests
{
    [Fact]
    public void Domain_does_not_define_business_report_data_or_file_entities()
    {
        var names = typeof(ReportDefinition).Assembly.GetTypes().Select(x => x.Name).ToHashSet();
        Assert.DoesNotContain("ReportData", names);
        Assert.DoesNotContain("ReportFile", names);
        Assert.DoesNotContain("DatasetVersion", names);
        Assert.DoesNotContain("SubmissionPackage", names);
    }

    [Fact]
    public void Sending_event_keeps_tracking_metadata_but_no_file_payload()
    {
        var properties = typeof(SendingEvent).GetProperties().Select(x => x.Name).ToHashSet();
        Assert.Contains(nameof(SendingEvent.ActualSentAtUtc), properties);
        Assert.Contains(nameof(SendingEvent.Reference), properties);
        Assert.DoesNotContain("File", properties);
        Assert.DoesNotContain("Content", properties);
    }
}
