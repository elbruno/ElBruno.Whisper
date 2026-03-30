using ElBruno.Whisper;

Console.WriteLine("🎤 ElBruno.Whisper — Hello Whisper Demo");
Console.WriteLine("========================================");
Console.WriteLine();

if (args.Length == 0)
{
    Console.WriteLine("Usage: HelloWhisper <audio-file.wav>");
    Console.WriteLine("  Transcribes a WAV audio file to text using Whisper.");
    Console.WriteLine();
    Console.WriteLine("Example:");
    Console.WriteLine("  HelloWhisper recording.wav");
    return;
}

var audioFile = args[0];

if (!File.Exists(audioFile))
{
    Console.WriteLine($"❌ File not found: {audioFile}");
    Console.WriteLine();
    Console.WriteLine("Please provide a valid path to an audio file (WAV, MP3, etc.)");
    return;
}

Console.WriteLine($"📂 Audio file: {audioFile}");
Console.WriteLine();

// Show model selection option
var model = KnownWhisperModels.WhisperTinyEn;
Console.WriteLine($"📦 Model: Whisper Tiny (English) — downloading if needed...");
Console.WriteLine();

// Track download progress
var downloadStarted = false;
var progress = new Progress<ElBruno.HuggingFace.DownloadProgress>(p =>
{
    if (p.Stage == ElBruno.HuggingFace.DownloadStage.Downloading)
    {
        if (!downloadStarted)
        {
            downloadStarted = true;
            Console.WriteLine("⬇️  Downloading model from HuggingFace...");
        }
        Console.Write($"\r  {p.CurrentFile}: {p.PercentComplete:F0}%    ");
    }
    else if (p.Stage == ElBruno.HuggingFace.DownloadStage.Validating)
    {
        if (downloadStarted)
            Console.WriteLine();
        Console.WriteLine($"  ✓ Validating model files...");
    }
    else if (p.Stage == ElBruno.HuggingFace.DownloadStage.Complete)
    {
        Console.WriteLine($"  ✓ Model ready!");
        Console.WriteLine();
    }
});

try
{
    // Create client and download model if needed
    using var client = await WhisperClient.CreateAsync(
        new WhisperOptions { Model = model },
        progress: progress);

    Console.WriteLine("🔊 Transcribing audio...");
    Console.WriteLine();

    // Transcribe the audio file
    var result = await client.TranscribeAsync(audioFile);

    // Display results
    Console.WriteLine("📝 Transcription Result:");
    Console.WriteLine("------------------------");
    Console.WriteLine(result.Text);
    Console.WriteLine();

    if (result.DetectedLanguage is not null)
    {
        Console.WriteLine($"🌍 Detected language: {result.DetectedLanguage}");
    }

    Console.WriteLine($"⏱️  Audio duration: {result.Duration.TotalSeconds:F1} seconds");
    Console.WriteLine();
    Console.WriteLine("✓ Transcription complete!");
}
catch (FileNotFoundException ex)
{
    Console.WriteLine($"❌ Error: Audio file not found");
    Console.WriteLine($"   {ex.Message}");
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"❌ Error during transcription");
    Console.WriteLine($"   {ex.Message}");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Unexpected error");
    Console.WriteLine($"   {ex.GetType().Name}: {ex.Message}");
}
