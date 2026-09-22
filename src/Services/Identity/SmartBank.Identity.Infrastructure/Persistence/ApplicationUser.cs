using Microsoft.AspNetCore.Identity;

namespace SmartBank.Identity.Infrastructure.Persistence;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public Guid? CustomerId { get; set; }
    public bool MfaEnabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
}

public sealed class ApplicationRole : IdentityRole<Guid>;
