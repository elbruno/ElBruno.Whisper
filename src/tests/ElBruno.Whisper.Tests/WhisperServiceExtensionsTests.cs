using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ElBruno.Whisper.Tests;

public class WhisperServiceExtensionsTests
{
    [Fact]
    public void AddWhisper_RegistersWhisperOptions()
    {
        var services = new ServiceCollection();
        
        services.AddWhisper();
        
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetService<WhisperOptions>();
        
        Assert.NotNull(options);
    }

    [Fact]
    public void AddWhisper_WithConfigureAction_AppliesConfiguration()
    {
        var services = new ServiceCollection();
        
        services.AddWhisper(options =>
        {
            options.Language = "es";
            options.Temperature = 0.5f;
            options.Translate = true;
        });
        
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<WhisperOptions>();
        
        Assert.Equal("es", options.Language);
        Assert.Equal(0.5f, options.Temperature);
        Assert.True(options.Translate);
    }

    [Fact]
    public void AddWhisper_WithConfigureAction_CanSetModel()
    {
        var services = new ServiceCollection();
        
        services.AddWhisper(options =>
        {
            options.Model = KnownWhisperModels.WhisperBase;
        });
        
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<WhisperOptions>();
        
        Assert.Equal("whisper-base", options.Model.Id);
    }

    [Fact]
    public void AddWhisper_WithConfigureAction_CanSetCacheDirectory()
    {
        var services = new ServiceCollection();
        var customCache = @"C:\custom\cache\path";
        
        services.AddWhisper(options =>
        {
            options.CacheDirectory = customCache;
        });
        
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<WhisperOptions>();
        
        Assert.Equal(customCache, options.CacheDirectory);
    }

    [Fact]
    public void AddWhisper_WithConfigureAction_CanSetMaxTokens()
    {
        var services = new ServiceCollection();
        
        services.AddWhisper(options =>
        {
            options.MaxTokens = 224;
        });
        
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<WhisperOptions>();
        
        Assert.Equal(224, options.MaxTokens);
    }

    [Fact]
    public void AddWhisper_RegistersSingletonOptions()
    {
        var services = new ServiceCollection();
        
        services.AddWhisper();
        
        var serviceProvider = services.BuildServiceProvider();
        var options1 = serviceProvider.GetRequiredService<WhisperOptions>();
        var options2 = serviceProvider.GetRequiredService<WhisperOptions>();
        
        Assert.Same(options1, options2);
    }

    [Fact]
    public void AddWhisper_WithoutConfigureAction_UsesDefaults()
    {
        var services = new ServiceCollection();
        
        services.AddWhisper();
        
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<WhisperOptions>();
        
        // Verify defaults
        Assert.Equal("whisper-tiny.en", options.Model.Id);
        Assert.True(options.EnsureModelDownloaded);
        Assert.Null(options.Language);
        Assert.Equal(0.0f, options.Temperature);
        Assert.Equal(448, options.MaxTokens);
        Assert.False(options.Translate);
    }
}
