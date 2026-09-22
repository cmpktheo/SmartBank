using MediatR;
using SmartBank.BuildingBlocks.Domain;

namespace SmartBank.BuildingBlocks.Application;

public interface ICommand<T> : IRequest<Result<T>>;
public interface ICommand : IRequest<Result>;
public interface IQuery<T> : IRequest<Result<T>>;

public interface ICommandHandler<TCommand, TResponse> : IRequestHandler<TCommand, Result<TResponse>>
    where TCommand : ICommand<TResponse>;
