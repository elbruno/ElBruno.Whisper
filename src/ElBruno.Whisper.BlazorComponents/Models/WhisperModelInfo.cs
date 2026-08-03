namespace ElBruno.Whisper.BlazorComponents.Models;

public sealed record WhisperModelInfo
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required WhisperModelSize Size { get; init; }
    public WhisperModelState State { get; init; }
    public double DownloadPercent { get; init; }
}
