using ElBruno.Whisper.BlazorComponents.Models;

namespace ElBruno.Whisper.BlazorComponents.Services;

public interface ITranscriptionHistoryStore
{
    IReadOnlyList<TranscriptionHistoryEntry> GetEntries(int maxItems);
    void AddEntry(TranscriptionHistoryEntry entry);
    void Clear();
}
