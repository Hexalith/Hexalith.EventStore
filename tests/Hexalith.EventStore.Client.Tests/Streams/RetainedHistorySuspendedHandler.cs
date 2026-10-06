using System.Net;

namespace Hexalith.EventStore.Client.Tests.Streams;

/// <summary>Ignores transport cancellation until its response is explicitly completed by the test.</summary>
/// <param name="suspendSend">Whether HTTP response headers remain pending.</param>
/// <param name="content">Owned transport content used by the response.</param>
internal sealed class RetainedHistorySuspendedHandler(bool suspendSend, HttpContent content) : HttpMessageHandler
{
    private readonly TaskCompletionSource<HttpResponseMessage> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly HttpResponseMessage _response = new(HttpStatusCode.OK) { Content = content };

    /// <summary>Signals that send has been invoked and can be cancelled by the test.</summary>
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets the still-pending HTTP operation.</summary>
    public Task Pending => _completion.Task;

    /// <summary>Supplies the late owned response without responding to the cancelled token.</summary>
    public void Complete() => _completion.TrySetResult(_response);

    /// <summary>Returns headers or a deliberately noncooperative pending send.</summary>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Started.TrySetResult();
        return suspendSend ? _completion.Task : Task.FromResult(_response);
    }
}
