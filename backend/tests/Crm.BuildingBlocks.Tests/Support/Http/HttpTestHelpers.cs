using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Crm.BuildingBlocks.Tests.Support.Http;

/// <summary>An inner handler that records what it was asked to send and answers 200.</summary>
public sealed class StubHandler : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
    }
}

/// <summary>DefaultHttpContext's response feature ignores OnStarting; this one records the callbacks so a test can
/// run them the way the server does when the response begins.</summary>
public sealed class RecordingResponseFeature : IHttpResponseFeature, IHttpResponseBodyFeature
{
    private readonly List<(Func<object, Task> Callback, object State)> _starting = [];

    public int StatusCode { get; set; } = 200;

    public string? ReasonPhrase { get; set; }

    public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();

    public Stream Body { get; set; } = new MemoryStream();

    public bool HasStarted { get; private set; }

    public Stream Stream => Body;

    public System.IO.Pipelines.PipeWriter Writer => System.IO.Pipelines.PipeWriter.Create(Body);

    public void OnStarting(Func<object, Task> callback, object state) => _starting.Add((callback, state));

    public void OnCompleted(Func<object, Task> callback, object state)
    {
    }

    public async Task RunStartingAsync()
    {
        HasStarted = true;

        foreach ((Func<object, Task> callback, object state) in _starting)
        {
            await callback(state);
        }
    }

    public void DisableBuffering()
    {
    }

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SendFileAsync(string path, long offset, long? count, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task CompleteAsync() => Task.CompletedTask;
}

/// <summary>A rejected rate-limit lease that reports a retry-after interval.</summary>
public sealed class FakeLease(TimeSpan? retryAfter) : System.Threading.RateLimiting.RateLimitLease
{
    public override bool IsAcquired => false;

    public override IEnumerable<string> MetadataNames =>
        retryAfter is null ? [] : [System.Threading.RateLimiting.MetadataName.RetryAfter.Name];

    public override bool TryGetMetadata(string metadataName, out object? metadata)
    {
        if (retryAfter is { } value && metadataName == System.Threading.RateLimiting.MetadataName.RetryAfter.Name)
        {
            metadata = value;
            return true;
        }

        metadata = null;
        return false;
    }
}

public static class HttpContexts
{
    public static DefaultHttpContext Anonymous() => new();

    public static DefaultHttpContext Authenticated(IEnumerable<Claim> claims, string authenticationType)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType)),
        };
        return context;
    }

    public static DefaultHttpContext WithHeader(this DefaultHttpContext context, string name, string value)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Request.Headers[name] = value;
        return context;
    }
}
