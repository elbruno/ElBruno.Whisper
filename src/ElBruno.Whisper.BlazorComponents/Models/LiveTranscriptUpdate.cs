namespace ElBruno.Whisper.BlazorComponents.Models;

public sealed record LiveTranscriptUpdate
{
    public required string Text { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public bool IsFinal { get; init; }
    public string? Language { get; init; }
}
