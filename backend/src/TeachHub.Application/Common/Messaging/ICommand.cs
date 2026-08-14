using MediatR;

namespace TeachHub.Application.Common.Messaging;

/// <summary>Marker không generic để TransactionBehavior nhận diện được command.</summary>
public interface IBaseCommand;

public interface ICommand<out TResponse> : IRequest<TResponse>, IBaseCommand;

public interface IQuery<out TResponse> : IRequest<TResponse>;

public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>;

public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>;
