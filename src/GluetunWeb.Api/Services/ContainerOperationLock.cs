namespace GluetunWeb.Api.Services;

/// <summary>
/// Serializes container lifecycle work (deploy/start/stop/delete) against the background reconciler,
/// so a repair pass can never land mid-operation and fight a user action — e.g. restarting the SOCKS5
/// sidecar during the window where a stop has taken it down but Gluetun is still running.
///
/// Container operations are infrequent and short, so a single global gate is sufficient.
/// </summary>
public sealed class ContainerOperationLock
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<IDisposable> AcquireAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        return new Releaser(_gate);
    }

    private sealed class Releaser(SemaphoreSlim gate) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                gate.Release();
        }
    }
}
