using System.Security.Claims;

namespace SmartBank.BuildingBlocks.Web;

public sealed class HttpCurrentUser : SmartBank.BuildingBlocks.Application.ICurrentUser
{
    public HttpCurrentUser(IHttpContextAccessor acc)
    {
        var p = acc.HttpContext?.User;
        var sub = p?.FindFirstValue(ClaimTypes.NameIdentifier) ?? p?.FindFirstValue("sub") ?? Guid.Empty.ToString();
        UserId = Guid.TryParse(sub, out var g) ? g : Guid.Empty;
        Email = p?.FindFirstValue(ClaimTypes.Email) ?? p?.FindFirstValue("email") ?? "";
        var cid = p?.FindFirstValue("customer_id");
        CustomerId = Guid.TryParse(cid, out var cg) ? cg : null;
        Roles = p?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray() ?? [];
    }

    public Guid UserId { get; }
    public Guid? CustomerId { get; }
    public string Email { get; }
    public string[] Roles { get; }
}
