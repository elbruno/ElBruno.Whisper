using ElBruno.Whisper.Tokenizer;
using Xunit;

namespace ElBruno.Whisper.Tests.Tokenizer;

public class WhisperTokenizerTimestampTests : IDisposable
{
    private readonly string _tokenizerPath;
    private readonly WhisperTokenizer _tokenizer;

    public WhisperTokenizerTimestampTests()
    {
        // Find the test tokenizer JSON relative to the test assembly
        _tokenizerPath = FindTestTokenizerPath();
        _tokenizer = new WhisperTokenizer(_tokenizerPath);
    }

    private static string FindTestTokenizerPath()
    {
        // Walk up from the test output directory to find testdata
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "testdata", "tokenizer", "test-tokenizer.json");
            if (File.Exists(candidate))
                return candidate;
            dir = Path.GetDirectoryName(dir);
        }

        throw new FileNotFoundException("Could not find testdata/tokenizer/test-tokenizer.json");
    }

    public void Dispose()
    {
        // No unmanaged resources to clean up
        GC.SuppressFinalize(this);
    }

    // --- IsTimestampToken tests ---

    [Fact]
    public void IsTimestampToken_NoTimestampsToken_ReturnsFalse()
    {
        // 50363 is the <|notimestamps|> token itself, not a timestamp
        Assert.False(_tokenizer.IsTimestampToken(50363));
    }

    [Fact]
    public void IsTimestampToken_FirstTimestampToken_ReturnsTrue()
    {
        // 50364 is the first timestamp token (0.00s)
        Assert.True(_tokenizer.IsTimestampToken(50364));
    }

    [Fact]
    public void IsTimestampToken_SecondTimestampToken_ReturnsTrue()
    {
        Assert.True(_tokenizer.IsTimestampToken(50365));
    }

    [Fact]
    public void IsTimestampToken_HighTimestampToken_ReturnsTrue()
    {
        // Token representing ~30 seconds
        Assert.True(_tokenizer.IsTimestampToken(50364 + 1500));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(50257)]  // <|endoftext|>
    [InlineData(50258)]  // <|startoftranscript|>
    [InlineData(50359)]  // <|transcribe|>
    [InlineData(50362)]
    [InlineData(50363)]  // <|notimestamps|>
    public void IsTimestampToken_NonTimestampIds_ReturnFalse(int tokenId)
    {
        Assert.False(_tokenizer.IsTimestampToken(tokenId));
    }

    [Theory]
    [InlineData(50364)]
    [InlineData(50365)]
    [InlineData(50414)]
    [InlineData(51000)]
    public void IsTimestampToken_TimestampIds_ReturnTrue(int tokenId)
    {
        Assert.True(_tokenizer.IsTimestampToken(tokenId));
    }

    // --- GetTimestamp tests ---

    [Fact]
    public void GetTimestamp_FirstToken_ReturnsZero()
    {
        // 50364 → index 0 → 0.00s
        var ts = _tokenizer.GetTimestamp(50364);

        Assert.Equal(TimeSpan.Zero, ts);
    }

    [Fact]
    public void GetTimestamp_SecondToken_Returns20ms()
    {
        // 50365 → index 1 → 0.02s
        var ts = _tokenizer.GetTimestamp(50365);

        Assert.Equal(TimeSpan.FromSeconds(0.02), ts);
    }

    [Fact]
    public void GetTimestamp_Token50414_ReturnsOneSecond()
    {
        // 50414 → index 50 → 50 * 0.02 = 1.00s
        var ts = _tokenizer.GetTimestamp(50414);

        Assert.Equal(TimeSpan.FromSeconds(1.0), ts);
    }

    [Fact]
    public void GetTimestamp_Token50464_ReturnsTwoSeconds()
    {
        // 50464 → index 100 → 100 * 0.02 = 2.00s
        var ts = _tokenizer.GetTimestamp(50464);

        Assert.Equal(TimeSpan.FromSeconds(2.0), ts);
    }

    [Fact]
    public void GetTimestamp_Token51864_ReturnsThirtySeconds()
    {
        // 51864 → index 1500 → 1500 * 0.02 = 30.00s
        var ts = _tokenizer.GetTimestamp(51864);

        Assert.Equal(TimeSpan.FromSeconds(30.0), ts);
    }

    [Theory]
    [InlineData(50364, 0.0)]
    [InlineData(50365, 0.02)]
    [InlineData(50374, 0.20)]
    [InlineData(50414, 1.00)]
    [InlineData(50464, 2.00)]
    [InlineData(50514, 3.00)]
    public void GetTimestamp_KnownValues_ReturnsExpectedSeconds(int tokenId, double expectedSeconds)
    {
        var ts = _tokenizer.GetTimestamp(tokenId);

        Assert.Equal(expectedSeconds, ts.TotalSeconds, precision: 10);
    }

    // --- NoTimestampsToken property ---

    [Fact]
    public void NoTimestampsToken_ReturnsExpectedValue()
    {
        Assert.Equal(50363, _tokenizer.NoTimestampsToken);
    }

    // --- DecodeWithTimestamps tests ---

    [Fact]
    public void DecodeWithTimestamps_EmptyTokens_ReturnsEmptyResult()
    {
        var (text, segments) = _tokenizer.DecodeWithTimestamps(Array.Empty<int>());

        Assert.Equal(string.Empty, text);
        Assert.Empty(segments);
    }

    [Fact]
    public void DecodeWithTimestamps_SingleSegment_ReturnsOneSegment()
    {
        // <0.00s> Hello world <1.00s>
        var tokenIds = new[]
        {
            50364,  // timestamp 0.00s
            31373,  // ĠHello
            995,    // Ġworld
            50414   // timestamp 1.00s
        };

        var (text, segments) = _tokenizer.DecodeWithTimestamps(tokenIds);

        Assert.Equal("Hello world", text);
        Assert.Single(segments);
        Assert.Equal(TimeSpan.FromSeconds(0.0), segments[0].Start);
        Assert.Equal(TimeSpan.FromSeconds(1.0), segments[0].End);
        Assert.Equal("Hello world", segments[0].Text);
    }

    [Fact]
    public void DecodeWithTimestamps_TwoSegments_ReturnsBoth()
    {
        // <0.00s> Hello <1.00s> <1.00s> world <2.00s>
        var tokenIds = new[]
        {
            50364,  // timestamp 0.00s
            31373,  // ĠHello
            50414,  // timestamp 1.00s
            50414,  // timestamp 1.00s (start of second segment)
            995,    // Ġworld
            50464   // timestamp 2.00s
        };

        var (text, segments) = _tokenizer.DecodeWithTimestamps(tokenIds);

        Assert.Equal("Hello world", text);
        Assert.Equal(2, segments.Count);

        Assert.Equal(TimeSpan.FromSeconds(0.0), segments[0].Start);
        Assert.Equal(TimeSpan.FromSeconds(1.0), segments[0].End);
        Assert.Equal("Hello", segments[0].Text);

        Assert.Equal(TimeSpan.FromSeconds(1.0), segments[1].Start);
        Assert.Equal(TimeSpan.FromSeconds(2.0), segments[1].End);
        Assert.Equal("world", segments[1].Text);
    }

    [Fact]
    public void DecodeWithTimestamps_SkipsSpecialTokens()
    {
        // SOT <0.00s> Hello <1.00s> EOT
        var tokenIds = new[]
        {
            50258,  // <|startoftranscript|>
            50364,  // timestamp 0.00s
            31373,  // ĠHello
            50414,  // timestamp 1.00s
            50257   // <|endoftext|>
        };

        var (text, segments) = _tokenizer.DecodeWithTimestamps(tokenIds);

        Assert.Equal("Hello", text);
        Assert.Single(segments);
        Assert.Equal("Hello", segments[0].Text);
    }

    [Fact]
    public void DecodeWithTimestamps_TextWithNoTimestamps_ReturnsEmptySegments()
    {
        // Just text tokens, no timestamps
        var tokenIds = new[]
        {
            31373,  // ĠHello
            995     // Ġworld
        };

        var (text, segments) = _tokenizer.DecodeWithTimestamps(tokenIds);

        Assert.Equal("Hello world", text);
        Assert.Empty(segments);
    }

    [Fact]
    public void DecodeWithTimestamps_TrailingTextAfterStartTimestamp_EmitsSegment()
    {
        // <0.00s> Hello (no end timestamp)
        var tokenIds = new[]
        {
            50364,  // timestamp 0.00s
            31373   // ĠHello
        };

        var (text, segments) = _tokenizer.DecodeWithTimestamps(tokenIds);

        Assert.Equal("Hello", text);
        Assert.Single(segments);
        // Trailing segments use the start time as the end time
        Assert.Equal(TimeSpan.FromSeconds(0.0), segments[0].Start);
        Assert.Equal(TimeSpan.FromSeconds(0.0), segments[0].End);
        Assert.Equal("Hello", segments[0].Text);
    }

    [Fact]
    public void DecodeWithTimestamps_WhitespaceOnlySegment_IsSkipped()
    {
        // <0.00s> <1.00s> — empty segment between timestamps should be skipped
        var tokenIds = new[]
        {
            50364,  // timestamp 0.00s
            50414   // timestamp 1.00s (end of empty segment)
        };

        var (text, segments) = _tokenizer.DecodeWithTimestamps(tokenIds);

        Assert.Equal(string.Empty, text);
        Assert.Empty(segments);
    }

    [Fact]
    public void DecodeWithTimestamps_MultipleTokensInSegment_ConcatenatesText()
    {
        // <0.00s> This is a test <2.00s>
        var tokenIds = new[]
        {
            50364,  // timestamp 0.00s
            770,    // ĠThis
            318,    // Ġis
            257,    // Ġa
            1332,   // Ġtest
            50464   // timestamp 2.00s
        };

        var (text, segments) = _tokenizer.DecodeWithTimestamps(tokenIds);

        Assert.Equal("This is a test", text);
        Assert.Single(segments);
        Assert.Equal("This is a test", segments[0].Text);
        Assert.Equal(TimeSpan.FromSeconds(0.0), segments[0].Start);
        Assert.Equal(TimeSpan.FromSeconds(2.0), segments[0].End);
    }

    [Fact]
    public void DecodeWithTimestamps_FullTranscriptSequence_ExtractsAllSegments()
    {
        // Simulates a real Whisper output:
        // SOT en transcribe <0.00s> Hello world <1.00s> <1.00s> This is a test <2.00s> EOT
        var tokenIds = new[]
        {
            50258,  // <|startoftranscript|>
            50259,  // <|en|>
            50359,  // <|transcribe|>
            50364,  // timestamp 0.00s
            31373,  // ĠHello
            995,    // Ġworld
            50414,  // timestamp 1.00s
            50414,  // timestamp 1.00s (start of segment 2)
            770,    // ĠThis
            318,    // Ġis
            257,    // Ġa
            1332,   // Ġtest
            50464,  // timestamp 2.00s
            50257   // <|endoftext|>
        };

        var (text, segments) = _tokenizer.DecodeWithTimestamps(tokenIds);

        Assert.Equal("Hello world This is a test", text);
        Assert.Equal(2, segments.Count);

        Assert.Equal(TimeSpan.FromSeconds(0.0), segments[0].Start);
        Assert.Equal(TimeSpan.FromSeconds(1.0), segments[0].End);
        Assert.Equal("Hello world", segments[0].Text);

        Assert.Equal(TimeSpan.FromSeconds(1.0), segments[1].Start);
        Assert.Equal(TimeSpan.FromSeconds(2.0), segments[1].End);
        Assert.Equal("This is a test", segments[1].Text);
    }
}
