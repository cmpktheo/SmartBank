using FluentValidation;
using MediatR;
using SmartBank.BuildingBlocks.Application;
using SmartBank.BuildingBlocks.Domain;

namespace SmartBank.BuildingBlocks.Application.Tests;

public sealed record PingCommand(string Name) : ICommand<string>;

public sealed class PingValidator : AbstractValidator<PingCommand>
{
    public PingValidator() => RuleFor(x => x.Name).NotEmpty();
}

public sealed class PingHandler : IRequestHandler<PingCommand, Result<string>>
{
    public Task<Result<string>> Handle(PingCommand request, CancellationToken ct)
        => Task.FromResult(Result.Success<string>(request.Name));
}

public sealed class ValidationBehaviorTests
{
    [Fact]
    public async Task Invalid_ReturnsValidationFailure()
    {
        var behavior = new ValidationBehavior<PingCommand, Result<string>>([new PingValidator()]);
        var result = await behavior.Handle(new PingCommand(""), () => Task.FromResult(Result.Success<string>("x")), CancellationToken.None);
        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION", result.Error.Code);
    }

    [Fact]
    public async Task Valid_CallsNext()
    {
        var behavior = new ValidationBehavior<PingCommand, Result<string>>([new PingValidator()]);
        var result = await behavior.Handle(new PingCommand("ok"), () => Task.FromResult(Result.Success<string>("ok")), CancellationToken.None);
        Assert.True(result.IsSuccess);
    }
}
