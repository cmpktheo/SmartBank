using System.Security.Cryptography;
using System.Text;

namespace SmartBank.Identity.Domain;

public sealed class OtpChallenge
{
    public Guid Id { get; }
    public Guid UserId { get; }
    public string CodeHash { get; private set; }
    public int Attempts { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public const int MaxAttempts = 5;

    private OtpChallenge(Guid id, Guid userId, string codeHash, DateTimeOffset expiresAt)
    {
        Id = id;
        UserId = userId;
        CodeHash = codeHash;
        ExpiresAt = expiresAt;
    }

    public static OtpChallenge Create(Guid userId, string code, DateTimeOffset now)
        => new(Guid.CreateVersion7(), userId, Hash(code, userId), now.AddMinutes(5));

    public static string GenerateCode()
    {
        var bytes = new byte[4];
        RandomNumberGenerator.Fill(bytes);
        var n = BitConverter.ToUInt32(bytes, 0) % 1_000_000;
        return n.ToString("D6");
    }

    public static string Hash(string code, Guid userId)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code + userId));
        return Convert.ToHexString(bytes);
    }

    public SmartBank.BuildingBlocks.Domain.Result Verify(string code, DateTimeOffset now)
    {
        if (now >= ExpiresAt)
            return SmartBank.BuildingBlocks.Domain.Result.Failure(
                SmartBank.BuildingBlocks.Domain.Error.Unauthorized("IDENTITY_MFA_EXPIRED", "Code expired."));
        if (Attempts >= MaxAttempts)
            return SmartBank.BuildingBlocks.Domain.Result.Failure(
                SmartBank.BuildingBlocks.Domain.Error.Unauthorized("IDENTITY_MFA_LOCKED", "Too many attempts."));
        if (!FixedTimeEquals(Hash(code, UserId), CodeHash))
        {
            Attempts++;
            if (Attempts >= MaxAttempts)
                return SmartBank.BuildingBlocks.Domain.Result.Failure(
                    SmartBank.BuildingBlocks.Domain.Error.Unauthorized("IDENTITY_MFA_LOCKED", "Too many attempts."));
            return SmartBank.BuildingBlocks.Domain.Result.Failure(
                SmartBank.BuildingBlocks.Domain.Error.Unauthorized("IDENTITY_MFA_INVALID", "Invalid code."));
        }
        return SmartBank.BuildingBlocks.Domain.Result.Success();
    }

    public void ResetCode(string newCode, DateTimeOffset now)
    {
        CodeHash = Hash(newCode, UserId);
        Attempts = 0;
        ExpiresAt = now.AddMinutes(5);
    }

    private static bool FixedTimeEquals(string a, string b)
        => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
