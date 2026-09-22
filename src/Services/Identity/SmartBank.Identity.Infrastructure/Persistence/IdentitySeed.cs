using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartBank.Identity.Application.Auth;
using SmartBank.Identity.Infrastructure.Persistence;

namespace SmartBank.Identity.Infrastructure.Persistence;

public sealed class EfLoginLookup : ILoginUserLookup, ILoginUserById
{
    private readonly UserManager<ApplicationUser> _users;
    public EfLoginLookup(UserManager<ApplicationUser> users) => _users = users;

    public async Task<LoginUser?> FindByEmailAsync(string email, CancellationToken ct)
    {
        // Support lookup by id string for MFA verify path.
        if (Guid.TryParse(email, out var id))
            return await FindByIdAsync(id, ct);
        var u = await _users.FindByEmailAsync(email);
        return u is null ? null : new LoginUser(u.Id, u.Email!, u.CustomerId, u.MfaEnabled, ["Customer"]);
    }

    public Task<LoginUser?> FindByIdAsync(Guid id, CancellationToken ct)
        => Task.FromResult(_users.Users.AsNoTracking().Where(u => u.Id == id)
            .Select(u => new LoginUser(u.Id, u.Email!, u.CustomerId, u.MfaEnabled, new[] { "Customer" }))
            .FirstOrDefault());

    public async Task<bool> CheckPasswordAsync(LoginUser user, string password, CancellationToken ct)
    {
        var u = await _users.FindByIdAsync(user.Id.ToString());
        if (u is null) return false;
        return await _users.CheckPasswordAsync(u, password);
    }

    public async Task<bool> IsLockedOutAsync(LoginUser user, CancellationToken ct)
    {
        var u = await _users.FindByIdAsync(user.Id.ToString());
        return u is not null && await _users.IsLockedOutAsync(u);
    }

    public async Task RecordFailureAsync(LoginUser user, CancellationToken ct)
    {
        var u = await _users.FindByIdAsync(user.Id.ToString());
        if (u is not null) await _users.AccessFailedAsync(u);
    }

    public async Task RecordSuccessAsync(LoginUser user, CancellationToken ct)
    {
        var u = await _users.FindByIdAsync(user.Id.ToString());
        if (u is not null)
        {
            await _users.ResetAccessFailedCountAsync(u);
            u.LastLoginAt = DateTimeOffset.UtcNow;
            await _users.UpdateAsync(u);
        }
    }
}

public static class IdentitySeed
{
    public static async Task RunAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        foreach (var role in new[] { "Customer", "Operations" })
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new ApplicationRole { Name = role });

        await EnsureUser(userManager, "alex.morgan@smartbank.test", "123456", Guid.Parse("11111111-1111-7111-1111-111111111111"));
        await EnsureUser(userManager, "jordan.lee@smartbank.test", "123456", Guid.Parse("22222222-2222-7222-2222-222222222222"));
    }

    private static async Task EnsureUser(UserManager<ApplicationUser> users, string email, string password, Guid customerId)
    {
        var existing = await users.FindByEmailAsync(email);
        if (existing is not null)
        {
            // Repair state left by previous failed seeds (null SecurityStamp, missing role/password).
            var repaired = false;
            if (string.IsNullOrEmpty(existing.SecurityStamp))
            {
                existing.SecurityStamp = Guid.NewGuid().ToString();
                repaired = true;
            }
            if (repaired)
            {
                var updateResult = await users.UpdateAsync(existing);
                if (!updateResult.Succeeded)
                    throw new InvalidOperationException($"Seed repair failed for {email}: {string.Join("; ", updateResult.Errors.Select(e => e.Description))}");
                existing = await users.FindByEmailAsync(email);
            }
            if (existing is not null && !await users.IsInRoleAsync(existing, "Customer"))
            {
                var roleResult = await users.AddToRoleAsync(existing, "Customer");
                if (!roleResult.Succeeded)
                    throw new InvalidOperationException($"Seed AddToRole failed for {email}: {string.Join("; ", roleResult.Errors.Select(e => e.Description))}");
            }
            if (existing is not null && !await users.CheckPasswordAsync(existing, password))
            {
                var token = await users.GeneratePasswordResetTokenAsync(existing);
                var resetResult = await users.ResetPasswordAsync(existing, token, password);
                if (!resetResult.Succeeded)
                    throw new InvalidOperationException($"Seed password reset failed for {email}: {string.Join("; ", resetResult.Errors.Select(e => e.Description))}");
            }
            return;
        }
        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            CustomerId = customerId,
            MfaEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            SecurityStamp = Guid.NewGuid().ToString()
        };
        var createResult = await users.CreateAsync(user, password);
        if (!createResult.Succeeded)
            throw new InvalidOperationException($"Seed CreateAsync failed for {email}: {string.Join("; ", createResult.Errors.Select(e => e.Description))}");
        var addRoleResult = await users.AddToRoleAsync(user, "Customer");
        if (!addRoleResult.Succeeded)
            throw new InvalidOperationException($"Seed AddToRole failed for {email}: {string.Join("; ", addRoleResult.Errors.Select(e => e.Description))}");
    }
}
