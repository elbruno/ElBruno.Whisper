namespace ElBruno.Whisper.Audio;

internal readonly record struct ProcessedAudio(float[] Samples, TimeSpan Duration);
