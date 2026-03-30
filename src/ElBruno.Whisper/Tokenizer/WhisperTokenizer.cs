using System.Text.Json;

namespace ElBruno.Whisper.Tokenizer;

/// <summary>
/// Tokenizer for decoding Whisper model outputs.
/// </summary>
internal sealed class WhisperTokenizer
{
    private readonly Dictionary<int, string> _idToToken = new();
    private readonly int _eotToken;

    public int EotToken => _eotToken;

    public WhisperTokenizer(string tokenizerJsonPath)
    {
        LoadTokenizer(tokenizerJsonPath);
        
        // Common Whisper special tokens
        _eotToken = FindTokenId("<|endoftext|>") ?? 50257;
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
        
        // If vocab not found in model, try added_tokens
        if (_idToToken.Count == 0 && root.TryGetProperty("added_tokens", out var addedTokens))
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
