using MediatR;

namespace Crm.BuildingBlocks.Tests.Support;

/// <summary>The <c>next</c> delegate of a pipeline behaviour, counting how often it ran.</summary>
public sealed class NextProbe<TResponse>
{
    private readonly Func<Task<TResponse>> _body;

    public NextProbe(TResponse response)
        : this(() => Task.FromResult(response))
    {
    }

    public NextProbe(Func<Task<TResponse>> body)
    {
        _body = body;
    }

    public int Calls { get; private set; }

    public bool WasCalled => Calls > 0;

    public RequestHandlerDelegate<TResponse> Next => () =>
    {
        Calls++;
        return _body();
    };

}

public static class NextProbe
{
    public static NextProbe<TResponse> Throwing<TResponse>(Exception exception) => new(() => throw exception);
}
