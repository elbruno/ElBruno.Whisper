namespace ElBruno.Whisper.BlazorComponents.Models;

public sealed record TranscriptionHistoryEntry
{
    public required Guid Id { get; init; }
    public required string SourceFileName { get; init; }
    public required string Transcript { get; init; }
    public TimeSpan Duration { get; init; }
    public string? Language { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
