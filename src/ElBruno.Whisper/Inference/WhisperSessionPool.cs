using System.Collections.Concurrent;
using System.Diagnostics;

namespace ElBruno.Whisper.Inference;

internal sealed class WhisperSessionPool : IDisposable
{
    private readonly SemaphoreSlim _concurrencyGate;
    private readonly ConcurrentBag<IWhisperInferenceSession> _availableSessions = new();
    private readonly Func<IWhisperInferenceSession> _sessionFactory;
    private readonly TimeSpan _queueTimeout;
    private readonly string _modelId;
    private readonly bool _enableSessionPooling;
    private bool _disposed;

    public WhisperSessionPool(
        string modelId,
        WhisperConcurrencyOptions options,
        Func<IWhisperInferenceSession> sessionFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(sessionFactory);

        _modelId = modelId ?? throw new ArgumentNullException(nameof(modelId));
        _sessionFactory = sessionFactory;
        _queueTimeout = options.QueueTimeout;
        _enableSessionPooling = options.EnableSessionPooling;
        MaximumConcurrentRequests = options.MaximumConcurrentRequests;
        _concurrencyGate = new SemaphoreSlim(MaximumConcurrentRequests, MaximumConcurrentRequests);
    }

    public int MaximumConcurrentRequests { get; }

    public bool SessionPoolingEnabled => _enableSessionPooling;

    public async ValueTask<WhisperSessionLease> AcquireAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        var waitTime = Stopwatch.StartNew();
        bool acquired;

        try
        {
            if (_queueTimeout == Timeout.InfiniteTimeSpan)
            {
                await _concurrencyGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                acquired = true;
            }
            else
            {
                acquired = await _concurrencyGate.WaitAsync(_queueTimeout, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            waitTime.Stop();
            WhisperMetrics.RecordQueueWait(
                _modelId,
                _enableSessionPooling,
                MaximumConcurrentRequests,
                waitTime.Elapsed,
                "cancelled");
            throw;
        }

        waitTime.Stop();
        WhisperMetrics.RecordQueueWait(
            _modelId,
            _enableSessionPooling,
            MaximumConcurrentRequests,
            waitTime.Elapsed,
            acquired ? "acquired" : "timed_out");

        if (!acquired)
        {
            throw new TimeoutException(
                $"Queued transcription request exceeded the configured timeout of {_queueTimeout}.");
        }

        try
        {
            var session = _enableSessionPooling && _availableSessions.TryTake(out var pooledSession)
                ? pooledSession
                : _sessionFactory();

            return new WhisperSessionLease(this, session);
        }
        catch
        {
            _concurrencyGate.Release();
            throw;
        }
    }

    internal void Return(IWhisperInferenceSession session)
    {
        try
        {
            if (_disposed || !_enableSessionPooling)
            {
                session.Dispose();
                return;
            }

            _availableSessions.Add(session);
        }
        finally
        {
            _concurrencyGate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        while (_availableSessions.TryTake(out var session))
        {
            session.Dispose();
        }

        _concurrencyGate.Dispose();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, typeof(WhisperSessionPool));
    }
}

internal sealed class WhisperSessionLease : IDisposable
{
    private WhisperSessionPool? _owner;
    private IWhisperInferenceSession? _session;

    public WhisperSessionLease(WhisperSessionPool owner, IWhisperInferenceSession session)
    {
        _owner = owner;
        _session = session;
    }

    public IWhisperInferenceSession Session => _session
        ?? throw new ObjectDisposedException(nameof(WhisperSessionLease));

    public void Dispose()
    {
        var owner = Interlocked.Exchange(ref _owner, null);
        var session = Interlocked.Exchange(ref _session, null);

        if (owner is null || session is null)
        {
            return;
        }

        owner.Return(session);
    }
}
