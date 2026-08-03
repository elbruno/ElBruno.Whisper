using ElBruno.Whisper.BlazorComponents.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ElBruno.Whisper.BlazorComponents.Extensions;

public static class WhisperBlazorComponentsServiceExtensions
{
    public static IServiceCollection AddWhisperBlazorComponents(this IServiceCollection services)
    {
        return services.AddWhisperBlazorComponents(_ => { });
    }

    public static IServiceCollection AddWhisperBlazorComponents(
        this IServiceCollection services,
        Action<WhisperBlazorComponentOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var options = new WhisperBlazorComponentOptions();
        configure(options);

        services.AddSingleton(options);
        services.AddSingleton<IWhisperService, WhisperService>();
        services.AddSingleton<IWhisperModelManager, WhisperModelManager>();
        services.AddSingleton<ITranscriptionHistoryStore, InMemoryTranscriptionHistoryStore>();

        return services;
    }
}
