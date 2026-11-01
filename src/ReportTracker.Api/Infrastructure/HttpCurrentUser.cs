using ReportTracker.Application.Abstractions;

namespace ReportTracker.Api.Infrastructure;

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid UserId => Guid.TryParse(accessor.HttpContext?.User.FindFirst("user_id")?.Value, out var id)
        ? id : Guid.Empty;
}
