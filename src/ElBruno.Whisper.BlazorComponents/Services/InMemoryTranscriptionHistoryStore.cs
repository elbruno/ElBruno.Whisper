using ElBruno.Whisper.BlazorComponents.Models;

namespace ElBruno.Whisper.BlazorComponents.Services;

public sealed class InMemoryTranscriptionHistoryStore(WhisperBlazorComponentOptions options) : ITranscriptionHistoryStore
{
    private readonly List<TranscriptionHistoryEntry> _entries = [];
    private readonly object _lock = new();

    public IReadOnlyList<TranscriptionHistoryEntry> GetEntries(int maxItems)
    {
        var cappedItems = Math.Max(1, maxItems);
        lock (_lock)
        {
            return _entries
                .OrderByDescending(e => e.CreatedAt)
                .Take(cappedItems)
                .ToList();
        }
    }

    public void AddEntry(TranscriptionHistoryEntry entry)
    {
        lock (_lock)
        {
            _entries.Add(entry);
            var maxCount = Math.Max(1, options.MaxHistoryItems);
            if (_entries.Count <= maxCount)
            {
                return;
            }

            var toRemove = _entries
                .OrderBy(e => e.CreatedAt)
                .Take(_entries.Count - maxCount)
                .ToList();

            foreach (var item in toRemove)
            {
                _entries.Remove(item);
            }
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
        }
    }
}
