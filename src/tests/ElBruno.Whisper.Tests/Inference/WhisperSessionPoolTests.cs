using ElBruno.Whisper.Inference;
using Xunit;

namespace ElBruno.Whisper.Tests.Inference;

public class WhisperSessionPoolTests
{
    [Fact]
    public async Task AcquireAsync_WithPoolingEnabled_ReusesReleasedSession()
    {
        var createdSessions = new List<FakeInferenceSession>();
        using var pool = CreatePool(
            new WhisperConcurrencyOptions
            {
                MaximumConcurrentRequests = 1,
                QueueTimeout = TimeSpan.FromSeconds(1),
                EnableSessionPooling = true
            },
            createdSessions);

        FakeInferenceSession firstSession;
        using (var firstLease = await pool.AcquireAsync(CancellationToken.None))
        {
            firstSession = (FakeInferenceSession)firstLease.Session;
        }

        using var secondLease = await pool.AcquireAsync(CancellationToken.None);
        var secondSession = (FakeInferenceSession)secondLease.Session;

        Assert.Same(firstSession, secondSession);
        Assert.Single(createdSessions);
        Assert.False(firstSession.Disposed);
    }

    [Fact]
    public async Task AcquireAsync_WithPoolingDisabled_DisposesReleasedSession()
    {
        var createdSessions = new List<FakeInferenceSession>();
        using var pool = CreatePool(
            new WhisperConcurrencyOptions
            {
                MaximumConcurrentRequests = 1,
                QueueTimeout = TimeSpan.FromSeconds(1),
                EnableSessionPooling = false
            },
            createdSessions);

        FakeInferenceSession firstSession;
        using (var firstLease = await pool.AcquireAsync(CancellationToken.None))
        {
            firstSession = (FakeInferenceSession)firstLease.Session;
        }

        using var secondLease = await pool.AcquireAsync(CancellationToken.None);
        var secondSession = (FakeInferenceSession)secondLease.Session;

        Assert.NotSame(firstSession, secondSession);
        Assert.True(firstSession.Disposed);
        Assert.Equal(2, createdSessions.Count);
    }

    [Fact]
    public async Task AcquireAsync_WhenWaitingCanBeCancelled_ThrowsOperationCanceledException()
    {
        using var pool = CreatePool(
            new WhisperConcurrencyOptions
            {
                MaximumConcurrentRequests = 1,
                QueueTimeout = Timeout.InfiniteTimeSpan
            },
            []);

        using var firstLease = await pool.AcquireAsync(CancellationToken.None);
        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await pool.AcquireAsync(cancellationSource.Token);
        });
    }

    [Fact]
    public async Task AcquireAsync_WhenQueueTimeoutExpires_ThrowsTimeoutException()
    {
        using var pool = CreatePool(
            new WhisperConcurrencyOptions
            {
                MaximumConcurrentRequests = 1,
                QueueTimeout = TimeSpan.FromMilliseconds(50)
            },
            []);

        using var firstLease = await pool.AcquireAsync(CancellationToken.None);

        await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await pool.AcquireAsync(CancellationToken.None);
        });
    }

    [Fact]
    public async Task AcquireAsync_WithConcurrentRequests_UsesSeparateSessions()
    {
        var createdSessions = new List<FakeInferenceSession>();
        using var pool = CreatePool(
            new WhisperConcurrencyOptions
            {
                MaximumConcurrentRequests = 2,
                QueueTimeout = TimeSpan.FromSeconds(1),
                EnableSessionPooling = true
            },
            createdSessions);

        using var firstLease = await pool.AcquireAsync(CancellationToken.None);
        using var secondLease = await pool.AcquireAsync(CancellationToken.None);

        var firstResult = firstLease.Session.Inference(
            [],
            [1],
            maxTokens: 8,
            eotToken: 0,
            cancellationToken: CancellationToken.None);
        var secondResult = secondLease.Session.Inference(
            [],
            [2],
            maxTokens: 8,
            eotToken: 0,
            cancellationToken: CancellationToken.None);

        Assert.NotSame(firstLease.Session, secondLease.Session);
        Assert.Equal(1, firstResult[0]);
        Assert.Equal(2, secondResult[0]);
        Assert.NotEqual(firstResult[1], secondResult[1]);
        Assert.Equal(2, createdSessions.Count);
    }

    private static WhisperSessionPool CreatePool(
        WhisperConcurrencyOptions options,
        List<FakeInferenceSession> createdSessions,
        TimeSpan? delay = null)
    {
        return new WhisperSessionPool(
            "whisper-tiny.en",
            options,
            () =>
            {
                var session = new FakeInferenceSession(
                    createdSessions.Count + 1,
                    delay ?? TimeSpan.Zero);
                createdSessions.Add(session);
                return session;
            });
    }
    private sealed class FakeInferenceSession : IWhisperInferenceSession
    {
        private readonly TimeSpan _delay;

        public FakeInferenceSession(int sessionId, TimeSpan delay)
        {
            SessionId = sessionId;
            _delay = delay;
        }

        public int SessionId { get; }

        public bool Disposed { get; private set; }

        public int[] Inference(
            float[] melSpectrogram,
            int[] initialTokens,
            int maxTokens,
            int eotToken,
            int[]? suppressTokens = null,
            int[]? beginSuppressTokens = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_delay > TimeSpan.Zero)
            {
                Task.Delay(_delay, cancellationToken).GetAwaiter().GetResult();
            }

            cancellationToken.ThrowIfCancellationRequested();
            return [initialTokens[0], SessionId];
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }
}
