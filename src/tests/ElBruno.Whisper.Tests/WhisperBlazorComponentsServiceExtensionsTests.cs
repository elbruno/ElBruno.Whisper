using ElBruno.Whisper.BlazorComponents.Extensions;
using ElBruno.Whisper.BlazorComponents.Models;
using ElBruno.Whisper.BlazorComponents.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ElBruno.Whisper.Tests;

public class WhisperBlazorComponentsServiceExtensionsTests
{
    [Fact]
    public void AddWhisperBlazorComponents_RegistersCoreServices()
    {
        var services = new ServiceCollection();
        services.AddWhisper();

        services.AddWhisperBlazorComponents();

        var provider = services.BuildServiceProvider();
        Assert.IsType<WhisperService>(provider.GetRequiredService<IWhisperService>());
        Assert.IsType<WhisperModelManager>(provider.GetRequiredService<IWhisperModelManager>());
        Assert.IsType<InMemoryTranscriptionHistoryStore>(provider.GetRequiredService<ITranscriptionHistoryStore>());
    }

    [Fact]
    public void AddWhisperBlazorComponents_UsesConfiguredOptions()
    {
        var services = new ServiceCollection();
        services.AddWhisper();

        services.AddWhisperBlazorComponents(options => { options.MaxHistoryItems = 3; });

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<WhisperBlazorComponentOptions>();

        Assert.Equal(3, options.MaxHistoryItems);
    }

    [Fact]
    public void InMemoryTranscriptionHistoryStore_RespectsMaxConfiguredEntries()
    {
        var store = new InMemoryTranscriptionHistoryStore(new WhisperBlazorComponentOptions { MaxHistoryItems = 2 });

        store.AddEntry(new TranscriptionHistoryEntry
        {
            Id = Guid.NewGuid(),
            SourceFileName = "one.wav",
            Transcript = "one",
            Duration = TimeSpan.FromSeconds(1),
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2)
        });

        store.AddEntry(new TranscriptionHistoryEntry
        {
            Id = Guid.NewGuid(),
            SourceFileName = "two.wav",
            Transcript = "two",
            Duration = TimeSpan.FromSeconds(2),
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        });

        store.AddEntry(new TranscriptionHistoryEntry
        {
            Id = Guid.NewGuid(),
            SourceFileName = "three.wav",
            Transcript = "three",
            Duration = TimeSpan.FromSeconds(3),
            CreatedAt = DateTimeOffset.UtcNow
        });

        var entries = store.GetEntries(maxItems: 10);

        Assert.Equal(2, entries.Count);
        Assert.DoesNotContain(entries, e => e.SourceFileName == "one.wav");
        Assert.Contains(entries, e => e.SourceFileName == "two.wav");
        Assert.Contains(entries, e => e.SourceFileName == "three.wav");
    }

    [Fact]
    public async Task WhisperModelManager_GetModelsAsync_ReturnsKnownModelEntries()
    {
        var manager = new WhisperModelManager(new WhisperOptions());

        var models = await manager.GetModelsAsync();

        Assert.NotEmpty(models);
        Assert.Contains(models, m => m.Id == KnownWhisperModels.WhisperTinyEn.Id);
    }
}
