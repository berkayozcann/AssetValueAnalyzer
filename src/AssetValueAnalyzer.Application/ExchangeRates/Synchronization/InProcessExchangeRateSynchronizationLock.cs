namespace AssetValueAnalyzer.Application.ExchangeRates.Synchronization;

public sealed class InProcessExchangeRateSynchronizationLock
    : IExchangeRateSynchronizationLock
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async ValueTask<IAsyncDisposable> AcquireAsync(
        CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        return new Releaser(_semaphore);
    }

    private sealed class Releaser(SemaphoreSlim semaphore) : IAsyncDisposable
    {
        private SemaphoreSlim? _semaphore = semaphore;

        public ValueTask DisposeAsync()
        {
            Interlocked.Exchange(ref _semaphore, null)?.Release();
            return ValueTask.CompletedTask;
        }
    }
}
