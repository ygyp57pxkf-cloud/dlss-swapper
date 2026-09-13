using System;
using System.Threading;
using System.Threading.Tasks;

namespace DLSS_Swapper.Helpers;

/// <summary>Optional artwork never owns the local game scan's readiness state.</summary>
internal sealed class BackgroundCoverRefresh
{
    static readonly SemaphoreSlim slots = new(4);
    int running;
    public Task Completion { get; private set; } = Task.CompletedTask;

    public void Start(Func<Task> refresh, Action<Exception> reportError)
    {
        if (Interlocked.CompareExchange(ref running, 1, 0) != 0) return;
        Completion = Task.Run(async () =>
        {
            await slots.WaitAsync().ConfigureAwait(false);
            try { await refresh().ConfigureAwait(false); }
            catch (Exception error) { reportError(error); }
            finally
            {
                slots.Release();
                Interlocked.Exchange(ref running, 0);
            }
        });
    }
}
