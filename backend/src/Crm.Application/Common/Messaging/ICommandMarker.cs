using Crm.Domain.Common.Results;
using MediatR;

namespace Crm.Application.Common.Messaging;

/// <summary>
/// Marks a request as a command so TransactionBehavior knows to open a transaction around it.
/// Queries do not implement this - without the marker every read would open and commit a
/// pointless SQL Server transaction on the hot path.
/// </summary>
public interface ICommandMarker;

public interface ICommand : IRequest<Result>, ICommandMarker;

public interface ICommand<TResponse> : IRequest<Result<TResponse>>, ICommandMarker;
