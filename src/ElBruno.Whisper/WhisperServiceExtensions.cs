using Microsoft.Extensions.DependencyInjection;

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

        return services;
    }
}
