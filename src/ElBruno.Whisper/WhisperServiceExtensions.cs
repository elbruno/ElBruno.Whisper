using Microsoft.Extensions.DependencyInjection;
#pragma warning disable MEAI001
using Microsoft.Extensions.AI;

namespace ElBruno.Whisper;

/// <summary>
/// Dependency injection extensions for WhisperClient.
/// </summary>
public static class WhisperServiceExtensions
{
    /// <summary>
    /// Add WhisperClient to the service collection with default options.
    /// </summary>
    public static IServiceCollection AddWhisper(this IServiceCollection services)
    {
        return AddWhisper(services, _ => { });
    }

    /// <summary>
    /// Add WhisperClient to the service collection with custom options.
    /// </summary>
    public static IServiceCollection AddWhisper(
        this IServiceCollection services,
        Action<WhisperOptions> configure)
    {
        var options = new WhisperOptions();
        configure(options);

        services.AddSingleton(options);
        services.AddSingleton<WhisperSpeechToTextClient>(static serviceProvider =>
            new WhisperSpeechToTextClient(serviceProvider.GetRequiredService<WhisperOptions>()));
        services.AddSingleton<ISpeechToTextClient>(static serviceProvider =>
            serviceProvider.GetRequiredService<WhisperSpeechToTextClient>());

        return services;
    }
}
