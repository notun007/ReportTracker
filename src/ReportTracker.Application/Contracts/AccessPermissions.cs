namespace ReportTracker.Application.Contracts;

public static class AccessPermissions
{
    public const string Users = "users.manage", Divisions = "divisions.manage", Roles = "roles.manage";
    public const string Reference = "administration.manage", Reports = "reports.manage", Cycles = "cycles.manage";
    public const string Components = "components.manage", Teams = "component.teams.manage", Responsibilities = "responsibilities.manage";
    public const string Monitor = "reports.monitor", EvidenceReview = "evidence.review", Extensions = "extensions.review", Reminders = "reminders.manage";
    public const string ComponentRead = "components.read", ComponentSubmit = "components.submit", Receive = "component.receive";
    public const string ReportRead = "reports.read", ReportSubmit = "report.submit", Upload = "evidence.upload";
    public const string LegacyRead = "tracking.read", LegacyRecord = "tracking.record";
    public static readonly string[] Configuration = [Reports, Cycles, Components, Responsibilities, Monitor, Reminders];
    public static readonly AccessPermission[] Catalog =
    [
        new(Users,"Manage users","Administration","Create, update, activate and deactivate accounts; reset passwords.",true),
        new(Divisions,"Manage divisions","Administration","Maintain organizational divisions.",true),
        new(Roles,"Manage roles and access","Administration","Maintain roles and assign scoped permissions to users.",true),
        new(Reference,"Manage reference data and automation","Administration","Manage recipients, frequencies, cycle presets and global automation.",true),
        new(Reports,"Manage report definitions","Report configuration","Create and maintain reports in scope."),
        new(Cycles,"Manage reporting cycles","Report configuration","Create and generate reporting cycles in scope."),
        new(Components,"Define components","Report configuration","Define components and preparing divisions."),
        new(Teams,"Manage component teams","Report configuration","Manage teams in the user's preparing division; explicit global grants cover all divisions."),
        new(Responsibilities,"Assign report responsibilities","Report configuration","Manage coordinating, receiving and final submitting assignments."),
        new(Monitor,"Monitor reports","Monitoring and review","View all period tasks for reports coordinated within scope."),
        new(EvidenceReview,"Review evidence","Monitoring and review","Review evidence while retaining the existing self-review restrictions."),
        new(Extensions,"Review deadline extensions","Monitoring and review","Approve or reject extension requests within scope."),
        new(Reminders,"Manage report reminders","Monitoring and review","Configure report reminder policies within scope."),
        new(ComponentRead,"View assigned components","Operational assignments","View components explicitly assigned to the user."),
        new(ComponentSubmit,"Record component submission","Operational assignments","Record sending only where existing preparer assignment rules permit it."),
        new(Receive,"Receive assigned components","Operational assignments","View incoming work and confirm receipt only with an active receiving assignment."),
        new(ReportRead,"View assigned final submissions","Operational assignments","View explicitly assigned final-submission tasks."),
        new(ReportSubmit,"Record final submission","Operational assignments","Record final submission for assigned reports after readiness checks pass."),
        new(Upload,"Upload evidence for assigned work","Operational assignments","Upload evidence for work the user is authorized to record."),
        new(LegacyRead,"Read tracking records within scope","Additional access","Explicit scoped monitoring access, including records not personally assigned."),
        new(LegacyRecord,"Record tracking within scope","Additional access","Explicit scoped recording access retained for existing authorized overrides.")
    ];
    public static readonly IReadOnlyDictionary<string,string[]> Defaults = new Dictionary<string,string[]>
    {
        ["SYS_ADMIN"] = [Users,Divisions,Roles,Reference],
        ["REPORT_ADMIN"] = [Reports,Cycles,Components,Teams,Responsibilities,Monitor,EvidenceReview,Extensions,Reminders,LegacyRead],
        ["REPORT_PREPARER"] = [ComponentRead,ComponentSubmit,Upload],
        ["COMPONENT_RECEIVER"] = [Receive],
        ["FINAL_SUBMITTER"] = [ReportRead,ReportSubmit,Upload]
    };
}
