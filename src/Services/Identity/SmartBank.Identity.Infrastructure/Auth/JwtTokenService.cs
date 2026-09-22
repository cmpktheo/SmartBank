using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SmartBank.Identity.Infrastructure.Persistence;

namespace SmartBank.Identity.Infrastructure.Auth;

public sealed class JwtTokenService : Application.Abstractions.ITokenService
{
    private readonly IdentityDbContext _db;
    private readonly IConfiguration _config;
    private readonly BuildingBlocks.Application.IClock _clock;

    public JwtTokenService(IdentityDbContext db, IConfiguration config, BuildingBlocks.Application.IClock clock)
    {
        _db = db;
        _config = config;
        _clock = clock;
    }

    public async Task<Application.Abstractions.TokenPair> IssueAsync(Guid userId, string email, Guid? customerId, string[] roles, CancellationToken ct)
    {
        var access = CreateAccessToken(userId, email, customerId, roles);
        var refresh = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            TokenHash = Hash(refresh),
            ExpiresAt = _clock.UtcNow.AddDays(14)
        });
        await _db.SaveChangesAsync(ct);
        return new Application.Abstractions.TokenPair(access, refresh, 900, customerId);
    }

    public async Task<Application.Abstractions.TokenPair> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var hash = Hash(refreshToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash, ct)
            ?? throw new UnauthorizedAccessException("Invalid refresh token.");
        if (stored.RevokedAt is not null || stored.ExpiresAt <= _clock.UtcNow)
            throw new UnauthorizedAccessException("Refresh token expired.");
        stored.RevokedAt = _clock.UtcNow;
        var user = await _db.Users.FirstAsync(u => u.Id == stored.UserId, ct);
        var roles = new[] { "Customer" };
        var access = CreateAccessToken(user.Id, user.Email!, user.CustomerId, roles);
        var next = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        stored.ReplacedBy = Hash(next);
        _db.RefreshTokens.Add(new RefreshToken { UserId = user.Id, TokenHash = Hash(next), ExpiresAt = _clock.UtcNow.AddDays(14) });
        await _db.SaveChangesAsync(ct);
        return new Application.Abstractions.TokenPair(access, next, 900, user.CustomerId);
    }

    public async Task RevokeRefreshAsync(string refreshToken, CancellationToken ct)
    {
        var hash = Hash(refreshToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash, ct);
        if (stored is not null)
        {
            stored.RevokedAt = _clock.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }

    private string CreateAccessToken(Guid userId, string email, Guid? customerId, string[] roles)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey()));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
            new("scope", "smartbank.api")
        };
        if (customerId.HasValue) claims.Add(new Claim("customer_id", customerId.Value.ToString()));
        foreach (var r in roles) claims.Add(new Claim(ClaimTypes.Role, r));
        claims.Add(new Claim("role", roles.FirstOrDefault() ?? "Customer"));
        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Authority"] ?? _config["IDENTITY_ISSUER"] ?? "http://localhost:5101",
            audience: "smartbank-spa",
            claims: claims,
            expires: _clock.UtcNow.AddMinutes(15).UtcDateTime,
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string SigningKey()
    {
        var key = _config["Jwt:SigningKey"] ?? _config["IDENTITY_SIGNING_KEY"];
        if (string.IsNullOrEmpty(key))
            key = "dev-signing-key-32-bytes-long!!!!!!";
        return key;
    }

    private static string Hash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
