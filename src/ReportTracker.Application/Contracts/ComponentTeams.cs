namespace ReportTracker.Application.Contracts;

public sealed record SaveComponentTeamMemberRequest(
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.MaxLength(4)] IReadOnlyList<string> Roles);

public sealed record ComponentTeamItem(Guid ReportId, string ReportCode, string ReportName, string PreparingDivision, ComponentItem Component);
public sealed record ComponentTeamMember(Guid Id, string Name, Guid DivisionId);
public sealed record ComponentTeamDirectory(IReadOnlyList<ComponentTeamItem> Components, IReadOnlyList<ComponentTeamMember> Members);
