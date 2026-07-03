using System.Text.Json;
using System.Text.RegularExpressions;

namespace ElBruno.Whisper.Tokenizer;

/// <summary>
/// Tokenizer for decoding Whisper model outputs.
/// </summary>
internal sealed class WhisperTokenizer
{
    private readonly Dictionary<int, string> _idToToken = new();
    private readonly int _eotToken;
    private readonly int _noTimestampsToken;

    /// <summary>
    /// The first timestamp token ID (noTimestamps + 1 = 50364).
    /// Each subsequent ID represents an additional 0.02 seconds.
    /// </summary>
    private const double SecondsPerTimestampToken = 0.02;

    public int EotToken => _eotToken;

    /// <summary>
    /// Token ID for the &lt;|notimestamps|&gt; special token.
    /// </summary>
    public int NoTimestampsToken => _noTimestampsToken;

    public WhisperTokenizer(string tokenizerJsonPath)
    {
        LoadTokenizer(tokenizerJsonPath);
        
        // Common Whisper special tokens
        _eotToken = FindTokenId("<|endoftext|>") ?? 50257;
        _noTimestampsToken = FindTokenId("<|notimestamps|>") ?? 50363;
    }

    private void LoadTokenizer(string path)
    {
        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        
        var root = doc.RootElement;
        
        // Extract vocabulary from model.vocab
        if (root.TryGetProperty("model", out var model) &&
            model.TryGetProperty("vocab", out var vocab))
        {
            foreach (var prop in vocab.EnumerateObject())
            {
                var token = prop.Name;
                var id = prop.Value.GetInt32();
                _idToToken[id] = token;
            }
        }
        
        // Always load added_tokens (special tokens like SOT, EOT, language, etc.)
        if (root.TryGetProperty("added_tokens", out var addedTokens))
        {
            foreach (var token in addedTokens.EnumerateArray())
            {
                if (token.TryGetProperty("id", out var idProp) &&
                    token.TryGetProperty("content", out var contentProp))
                {
                    _idToToken[idProp.GetInt32()] = contentProp.GetString() ?? "";
                }
            }
        }
    }

    /// <summary>
    /// Decode token IDs to text.
    /// </summary>
    public string Decode(int[] tokenIds)
    {
        var tokens = new List<string>();
        
        foreach (var id in tokenIds)
        {
            if (_idToToken.TryGetValue(id, out var token))
            {
                // Skip special tokens
                if (token.StartsWith("<|") && token.EndsWith("|>"))
                    continue;
                
                tokens.Add(token);
            }
        }
        
        // Join tokens and clean up byte-pair encoding artifacts
        var text = string.Join("", tokens);
        text = text.Replace("Ġ", " "); // GPT-2 style space encoding
        text = text.Replace("Ċ", "\n"); // Newline encoding
        
        return text.Trim();
    }

    /// <summary>
    /// Returns true if the token ID is a timestamp token (>= noTimestamps + 1).
    /// </summary>
    public bool IsTimestampToken(int tokenId) => tokenId >= _noTimestampsToken + 1;

    /// <summary>
    /// Converts a timestamp token ID to its corresponding time offset.
    /// Token 50364 = 0.00s, 50365 = 0.02s, etc.
    /// </summary>
    public TimeSpan GetTimestamp(int tokenId)
    {
        var index = tokenId - (_noTimestampsToken + 1);
        return TimeSpan.FromSeconds(index * SecondsPerTimestampToken);
    }

    /// <summary>
    /// Decodes token IDs into text and timestamped segments.
    /// Timestamp tokens appear in pairs: start timestamp, text tokens, end timestamp.
    /// </summary>
    public (string Text, List<TranscriptionSegment> Segments) DecodeWithTimestamps(int[] tokenIds)
    {
        var segments = new List<TranscriptionSegment>();
        var allTextTokens = new List<string>();

        TimeSpan? pendingStart = null;
        var currentSegmentTokens = new List<string>();

        foreach (var id in tokenIds)
        {
            if (IsTimestampToken(id))
            {
                var ts = GetTimestamp(id);

                if (pendingStart is null)
                {
                    // This is a start timestamp
                    pendingStart = ts;
                    currentSegmentTokens.Clear();
                }
                else
                {
                    // This is an end timestamp — emit segment
                    var segmentText = DecodeTextTokens(currentSegmentTokens);
                    if (!string.IsNullOrWhiteSpace(segmentText))
                    {
                        segments.Add(CreateTimedSegment(pendingStart.Value, ts, segmentText));
                    }

                    pendingStart = null;
                    currentSegmentTokens.Clear();
                }
            }
            else if (_idToToken.TryGetValue(id, out var token))
            {
                // Skip non-timestamp special tokens
                if (token.StartsWith("<|") && token.EndsWith("|>"))
                    continue;

                currentSegmentTokens.Add(token);
                allTextTokens.Add(token);
            }
        }

        // Handle trailing text tokens with a pending start but no end timestamp
        if (pendingStart is not null && currentSegmentTokens.Count > 0)
        {
            var segmentText = DecodeTextTokens(currentSegmentTokens);
            if (!string.IsNullOrWhiteSpace(segmentText))
            {
                segments.Add(CreateTimedSegment(pendingStart.Value, pendingStart.Value, segmentText));
            }
        }

        var fullText = DecodeTextTokens(allTextTokens);
        return (fullText, segments);
    }

    public TranscriptionSegment CreateTimedSegment(TimeSpan start, TimeSpan end, string text)
    {
        return new TranscriptionSegment
        {
            Start = start,
            End = end,
            Text = text,
            Words = BuildWords(start, end, text)
        };
    }

    /// <summary>
    /// Joins raw BPE tokens and cleans up encoding artifacts.
    /// </summary>
    private static string DecodeTextTokens(List<string> tokens)
    {
        var text = string.Join("", tokens);
        text = text.Replace("Ġ", " ");
        text = text.Replace("Ċ", "\n");
        return text.Trim();
    }

    private static IReadOnlyList<TranscriptionWord> BuildWords(TimeSpan start, TimeSpan end, string segmentText)
    {
        var matches = Regex.Matches(segmentText, @"\S+");
        if (matches.Count == 0)
        {
            return Array.Empty<TranscriptionWord>();
        }

        if (matches.Count == 1 || end <= start)
        {
            return matches
                .Select(match => new TranscriptionWord
                {
                    Start = start,
                    End = end,
                    Text = match.Value
                })
                .ToArray();
        }

        var duration = end - start;
        var weights = matches
            .Select(match => Math.Max(1, match.Value.Count(static ch => !char.IsWhiteSpace(ch))))
            .ToArray();
        var totalWeight = weights.Sum();
        var words = new List<TranscriptionWord>(matches.Count);
        var consumedWeight = 0;

        for (var i = 0; i < matches.Count; i++)
        {
            var wordStart = start + ScaleDuration(duration, consumedWeight, totalWeight);
            consumedWeight += weights[i];
            var wordEnd = i == matches.Count - 1
                ? end
                : start + ScaleDuration(duration, consumedWeight, totalWeight);

            words.Add(new TranscriptionWord
            {
                Start = wordStart,
                End = wordEnd,
                Text = matches[i].Value
            });
        }

        return words;
    }

    private static TimeSpan ScaleDuration(TimeSpan duration, int consumedWeight, int totalWeight)
    {
        if (consumedWeight <= 0 || totalWeight <= 0 || duration <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        var scaledTicks = (long)Math.Round(duration.Ticks * (consumedWeight / (double)totalWeight));
        return TimeSpan.FromTicks(scaledTicks);
    }

    /// <summary>
    /// Find token ID by token string.
    /// </summary>
    public int? FindTokenId(string token)
    {
        foreach (var kvp in _idToToken)
        {
            if (kvp.Value == token)
                return kvp.Key;
        }
        return null;
    }

    /// <summary>
    /// Get special token IDs for transcription/translation.
    /// </summary>
    public (int startOfTranscript, int transcribe, int translate, int noTimestamps, int? language) GetSpecialTokenIds(string? languageCode = null)
    {
        var sot = FindTokenId("<|startoftranscript|>") ?? 50258;
        var transcribe = FindTokenId("<|transcribe|>") ?? 50359;
        var translate = FindTokenId("<|translate|>") ?? 50358;
        var noTimestamps = FindTokenId("<|notimestamps|>") ?? 50363;
        
        int? language = null;
        if (!string.IsNullOrEmpty(languageCode))
        {
            language = FindTokenId($"<|{languageCode}|>");
        }
        
        return (sot, transcribe, translate, noTimestamps, language);
    }
}
