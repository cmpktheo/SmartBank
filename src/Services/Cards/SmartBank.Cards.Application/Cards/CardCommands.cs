using MediatR;
using SmartBank.BuildingBlocks.Application;
using SmartBank.BuildingBlocks.Application.Metrics;
using SmartBank.BuildingBlocks.Domain;
using SmartBank.BuildingBlocks.Domain.ValueObjects;
using SmartBank.Cards.Domain;

namespace SmartBank.Cards.Application.Cards;

public sealed record CardDto(Guid Id, Guid AccountId, string Type, string Brand, string MaskedPan, string LastFour, int ExpiryMonth, int ExpiryYear, string Status, string DailyEcommerceLimit, string DailyAtmLimit, string Currency);

public interface ICardRepository
{
    Task<Card?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<List<Card>> ListByCustomerAsync(Guid customerId, CancellationToken ct);
    Task<Card?> GetByAccountAsync(Guid accountId, CancellationToken ct);
    Task AddAsync(Card card, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

public sealed record FreezeCardCommand(Guid CardId, string IdempotencyKey) : ICommand<CardDto>;
public sealed record UnfreezeCardCommand(Guid CardId, string IdempotencyKey) : ICommand<CardDto>;
public sealed record SetCardLimitsCommand(Guid CardId, decimal Ecom, decimal Atm) : ICommand<CardDto>;
public sealed record GetCardsQuery() : IQuery<List<CardDto>>;
public sealed record GetCardQuery(Guid CardId) : IQuery<CardDto>;

public static class CardMapper
{
    public static CardDto ToDto(Card c) => new(c.Id, c.AccountId, c.Type.ToString(), c.Brand.ToString(), $"•••• •••• •••• {c.LastFour}", c.LastFour,
        c.ExpiryMonth, c.ExpiryYear, c.Status.ToString(),
        c.DailyEcommerceLimit.Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
        c.DailyAtmLimit.Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), c.Currency);
}

public sealed class FreezeCardHandler : IRequestHandler<FreezeCardCommand, Result<CardDto>>
{
    private readonly ICardRepository _repo;
    private readonly ICurrentUser _user;
    public FreezeCardHandler(ICardRepository repo, ICurrentUser user) { _repo = repo; _user = user; }
    public async Task<Result<CardDto>> Handle(FreezeCardCommand req, CancellationToken ct)
    {
        var c = await _repo.GetByIdAsync(req.CardId, ct);
        if (c is null)
        {
            SmartBankMeters.CardOp("freeze", "not_found");
            return Result.Failure<CardDto>(Error.NotFound("CARD_NOT_FOUND", "Card not found."));
        }
        if (_user.CustomerId != c.CustomerId)
        {
            SmartBankMeters.CardOp("freeze", "forbidden");
            return Result.Failure<CardDto>(Error.Forbidden("CARD_NOT_OWNED", "Not your card."));
        }
        var r = c.Freeze();
        if (r.IsFailure)
        {
            SmartBankMeters.CardOp("freeze", "failed");
            return Result.Failure<CardDto>(r.Error);
        }
        await _repo.SaveChangesAsync(ct);
        SmartBankMeters.CardOp("freeze", "success");
        return Result.Success(CardMapper.ToDto(c));
    }
}

public sealed class UnfreezeCardHandler : IRequestHandler<UnfreezeCardCommand, Result<CardDto>>
{
    private readonly ICardRepository _repo;
    private readonly ICurrentUser _user;
    public UnfreezeCardHandler(ICardRepository repo, ICurrentUser user) { _repo = repo; _user = user; }
    public async Task<Result<CardDto>> Handle(UnfreezeCardCommand req, CancellationToken ct)
    {
        var c = await _repo.GetByIdAsync(req.CardId, ct);
        if (c is null)
        {
            SmartBankMeters.CardOp("unfreeze", "not_found");
            return Result.Failure<CardDto>(Error.NotFound("CARD_NOT_FOUND", "Card not found."));
        }
        if (_user.CustomerId != c.CustomerId)
        {
            SmartBankMeters.CardOp("unfreeze", "forbidden");
            return Result.Failure<CardDto>(Error.Forbidden("CARD_NOT_OWNED", "Not your card."));
        }
        var r = c.Unfreeze();
        if (r.IsFailure)
        {
            SmartBankMeters.CardOp("unfreeze", "failed");
            return Result.Failure<CardDto>(r.Error);
        }
        await _repo.SaveChangesAsync(ct);
        SmartBankMeters.CardOp("unfreeze", "success");
        return Result.Success(CardMapper.ToDto(c));
    }
}

public sealed class SetLimitsHandler : IRequestHandler<SetCardLimitsCommand, Result<CardDto>>
{
    private readonly ICardRepository _repo;
    private readonly ICurrentUser _user;
    public SetLimitsHandler(ICardRepository repo, ICurrentUser user) { _repo = repo; _user = user; }
    public async Task<Result<CardDto>> Handle(SetCardLimitsCommand req, CancellationToken ct)
    {
        var c = await _repo.GetByIdAsync(req.CardId, ct);
        if (c is null)
        {
            SmartBankMeters.CardOp("set_limits", "not_found");
            return Result.Failure<CardDto>(Error.NotFound("CARD_NOT_FOUND", "Card not found."));
        }
        if (_user.CustomerId != c.CustomerId)
        {
            SmartBankMeters.CardOp("set_limits", "forbidden");
            return Result.Failure<CardDto>(Error.Forbidden("CARD_NOT_OWNED", "Not your card."));
        }
        var cur = Currency.From(c.Currency);
        var r = c.SetLimits(Money.Of(req.Ecom, cur), Money.Of(req.Atm, cur));
        if (r.IsFailure)
        {
            SmartBankMeters.CardOp("set_limits", "failed");
            return Result.Failure<CardDto>(r.Error);
        }
        await _repo.SaveChangesAsync(ct);
        SmartBankMeters.CardOp("set_limits", "success");
        return Result.Success(CardMapper.ToDto(c));
    }
}

public sealed class GetCardsHandler : IRequestHandler<GetCardsQuery, Result<List<CardDto>>>
{
    private readonly ICardRepository _repo;
    private readonly ICurrentUser _user;
    public GetCardsHandler(ICardRepository repo, ICurrentUser user) { _repo = repo; _user = user; }
    public async Task<Result<List<CardDto>>> Handle(GetCardsQuery req, CancellationToken ct)
    {
        if (_user.CustomerId is null) return Result.Failure<List<CardDto>>(Error.Forbidden("CARD_NOT_OWNED", "No customer."));
        var list = await _repo.ListByCustomerAsync(_user.CustomerId.Value, ct);
        return Result.Success(list.Select(CardMapper.ToDto).ToList());
    }
}

public sealed class GetCardHandler : IRequestHandler<GetCardQuery, Result<CardDto>>
{
    private readonly ICardRepository _repo;
    private readonly ICurrentUser _user;
    public GetCardHandler(ICardRepository repo, ICurrentUser user) { _repo = repo; _user = user; }
    public async Task<Result<CardDto>> Handle(GetCardQuery req, CancellationToken ct)
    {
        var c = await _repo.GetByIdAsync(req.CardId, ct);
        if (c is null) return Result.Failure<CardDto>(Error.NotFound("CARD_NOT_FOUND", "Card not found."));
        if (_user.CustomerId != c.CustomerId) return Result.Failure<CardDto>(Error.Forbidden("CARD_NOT_OWNED", "Not your card."));
        return Result.Success(CardMapper.ToDto(c));
    }
}
