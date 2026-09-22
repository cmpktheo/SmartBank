namespace SmartBank.BuildingBlocks.Application;

public interface ICurrentUser
{
    Guid UserId { get; }
    Guid? CustomerId { get; }
    string Email { get; }
    string[] Roles { get; }
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
