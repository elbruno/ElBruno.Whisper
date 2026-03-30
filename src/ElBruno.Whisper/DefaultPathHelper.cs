namespace ElBruno.Whisper;

/// <summary>
/// Helper for default cache directory paths.
/// </summary>
internal static class DefaultPathHelper
{
    /// <summary>
    /// Get default cache directory: %LOCALAPPDATA%/ElBruno/{product}/models on Windows.
    /// </summary>
    public static string GetDefaultCacheDirectory(string product)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        
        if (string.IsNullOrEmpty(localAppData))
        {
            // Fallback to user profile
            localAppData = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        var cacheDir = Path.Combine(localAppData, product, "models");
        Directory.CreateDirectory(cacheDir);
        
        return cacheDir;
    }
}
