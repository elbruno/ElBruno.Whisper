#pragma warning disable MEAI001
using System.Globalization;
using System.Runtime.CompilerServices;
using ElBruno.Whisper.Audio;
using Microsoft.Extensions.AI;

namespace ElBruno.Whisper;

/// <summary>
/// Adapts <see cref="WhisperClient"/> to the Microsoft.Extensions.AI speech-to-text abstraction.
/// </summary>
public sealed class WhisperSpeechToTextClient : ISpeechToTextClient
{
    /// <summary>
    /// Metadata key for the detected language returned by Whisper.
    /// </summary>
    public const string DetectedLanguageMetadataKey = "elbruno.whisper.detected_language";

    /// <summary>
    /// Metadata key for the processed audio duration, in milliseconds.
    /// </summary>
    public const string AudioDurationMetadataKey = "elbruno.whisper.audio_duration_ms";

    /// <summary>
    /// Metadata key for timestamped transcription segments.
    /// </summary>
    public const string SegmentsMetadataKey = "elbruno.whisper.segments";

    /// <summary>
    /// Metadata key for flattened timestamped words.
    /// </summary>
    public const string WordsMetadataKey = "elbruno.whisper.words";

    /// <summary>
    /// Metadata key for the Whisper model identifier used for the request.
    /// </summary>
    public const string ModelIdMetadataKey = "elbruno.whisper.model_id";

    /// <summary>
    /// Metadata key for the execution provider.
    /// </summary>
    public const string ExecutionProviderMetadataKey = "elbruno.whisper.execution_provider";

    private const string ProviderName = "elbruno.whisper";
    private static readonly Uri ProviderUri = new("https://github.com/elbruno/ElBruno.Whisper");
    private readonly Func<WhisperOptions, CancellationToken, Task<WhisperClient>> _clientFactory;
    private readonly WhisperOptions _defaultOptions;
    private readonly SpeechToTextClientMetadata _metadata;
    private readonly object _sharedClientGate = new();
    private WhisperClient? _innerClient;
    private Task<WhisperClient>? _sharedClientTask;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="WhisperSpeechToTextClient"/> class.
    /// </summary>
    /// <param name="inner">The Whisper client to adapt.</param>
    public WhisperSpeechToTextClient(WhisperClient inner)
    {
        ArgumentNullException.ThrowIfNull(inner);

        _innerClient = inner;
        _defaultOptions = CloneWhisperOptions(inner.Options);
        _clientFactory = static (options, cancellationToken) => WhisperClient.CreateAsync(options, cancellationToken: cancellationToken);
        _metadata = CreateMetadata(_defaultOptions);
    }

    internal WhisperSpeechToTextClient(WhisperOptions defaultOptions)
        : this(
            defaultOptions,
            static (options, cancellationToken) => WhisperClient.CreateAsync(options, cancellationToken: cancellationToken))
    {
    }

    internal WhisperSpeechToTextClient(
        WhisperOptions defaultOptions,
        Func<WhisperOptions, CancellationToken, Task<WhisperClient>> clientFactory)
    {
        ArgumentNullException.ThrowIfNull(defaultOptions);
        ArgumentNullException.ThrowIfNull(clientFactory);

        _defaultOptions = CloneWhisperOptions(defaultOptions);
        _clientFactory = clientFactory;
        _metadata = CreateMetadata(_defaultOptions);
    }

    /// <inheritdoc />
    public async Task<SpeechToTextResponse> GetTextAsync(
        Stream audioSpeechStream,
        SpeechToTextOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audioSpeechStream);
        ThrowIfDisposed();

        var requestOptions = CreateRequestOptions(options);
        var audioFormat = CreateAudioFormat(options);
        var useSharedClient = CanUseSharedClient(requestOptions);
        WhisperClient? ephemeralClient = null;

        try
        {
            var client = useSharedClient
                ? await GetSharedClientAsync(cancellationToken).ConfigureAwait(false)
                : ephemeralClient = await _clientFactory(CloneWhisperOptions(requestOptions), cancellationToken).ConfigureAwait(false);

            var result = audioFormat is WhisperAudioFormat format
                ? await client.TranscribeAsync(audioSpeechStream, format, cancellationToken).ConfigureAwait(false)
                : await client.TranscribeAsync(audioSpeechStream, cancellationToken).ConfigureAwait(false);

            return CreateResponse(result, requestOptions.Model.Id);
        }
        finally
        {
            ephemeralClient?.Dispose();
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<SpeechToTextResponseUpdate> GetStreamingTextAsync(
        Stream audioSpeechStream,
        SpeechToTextOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetTextAsync(audioSpeechStream, options, cancellationToken).ConfigureAwait(false);
        foreach (var update in response.ToSpeechToTextResponseUpdates())
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return update;
        }
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        if (serviceKey is not null)
        {
            return null;
        }

        if (serviceType == typeof(ISpeechToTextClient) || serviceType == typeof(WhisperSpeechToTextClient))
        {
            return this;
        }

        if (serviceType == typeof(SpeechToTextClientMetadata))
        {
            return _metadata;
        }

        if (serviceType == typeof(WhisperOptions))
        {
            return CloneWhisperOptions(_defaultOptions);
        }

        if (_sharedClientTask is { IsCompletedSuccessfully: true } sharedClientTaskResult &&
            serviceType.IsInstanceOfType(sharedClientTaskResult.Result))
        {
            return sharedClientTaskResult.Result;
        }

        return _innerClient is not null && serviceType.IsInstanceOfType(_innerClient)
            ? _innerClient
            : null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _innerClient?.Dispose();
        _innerClient = null;

        if (_sharedClientTask is { IsCompletedSuccessfully: true } sharedClientTask)
        {
            sharedClientTask.Result.Dispose();
        }

        _sharedClientTask = null;
    }

    private async Task<WhisperClient> GetSharedClientAsync(CancellationToken cancellationToken)
    {
        if (_innerClient is not null)
        {
            return _innerClient;
        }

        Task<WhisperClient>? sharedClientTask;
        lock (_sharedClientGate)
        {
            sharedClientTask = _sharedClientTask;
            if (sharedClientTask is null)
            {
                sharedClientTask = _clientFactory(CloneWhisperOptions(_defaultOptions), CancellationToken.None);
                _sharedClientTask = sharedClientTask;
            }
        }

        try
        {
            return await sharedClientTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            lock (_sharedClientGate)
            {
                if (ReferenceEquals(_sharedClientTask, sharedClientTask) && sharedClientTask.IsFaulted)
                {
                    _sharedClientTask = null;
                }
            }

            throw;
        }
    }

    private WhisperOptions CreateRequestOptions(SpeechToTextOptions? options)
    {
        var requestOptions = options?.RawRepresentationFactory?.Invoke(this) switch
        {
            null => CloneWhisperOptions(_defaultOptions),
            WhisperOptions whisperOptions => CloneWhisperOptions(whisperOptions),
            var unsupported => throw new InvalidOperationException(
                $"The supplied {nameof(SpeechToTextOptions.RawRepresentationFactory)} returned '{unsupported.GetType().FullName}', but '{typeof(WhisperOptions).FullName}' is required.")
        };

        if (options is null)
        {
            return requestOptions;
        }

        if (!string.IsNullOrWhiteSpace(options.ModelId))
        {
            requestOptions.Model = KnownWhisperModels.FindById(options.ModelId)
                ?? throw new ArgumentException($"Unknown Whisper model '{options.ModelId}'.", nameof(options));
        }

        if (!string.IsNullOrWhiteSpace(options.SpeechLanguage))
        {
            requestOptions.Language = options.SpeechLanguage;
        }

        requestOptions.Translate = ShouldTranslate(options, requestOptions.Language, requestOptions.Translate);
        ApplyAdditionalProperties(requestOptions, options.AdditionalProperties);

        return requestOptions;
    }

    private static WhisperAudioFormat? CreateAudioFormat(SpeechToTextOptions? options)
    {
        if (options is null)
        {
            return null;
        }

        var additionalProperties = options.AdditionalProperties;
        var mediaType = GetFirstString(additionalProperties, "elbruno.whisper.media_type", "mediaType", "mimeType");
        var channels = GetFirstInt32(additionalProperties, "elbruno.whisper.channels", "channels");
        var sampleFormatText = GetFirstString(additionalProperties, "elbruno.whisper.sample_format", "sampleFormat");
        var rawAudioRequested =
            channels.HasValue ||
            !string.IsNullOrWhiteSpace(sampleFormatText) ||
            (mediaType is not null && !mediaType.Equals("audio/wav", StringComparison.OrdinalIgnoreCase) && !mediaType.Equals("audio/x-wav", StringComparison.OrdinalIgnoreCase));

        if (!rawAudioRequested)
        {
            return null;
        }

        var sampleRate = options.SpeechSampleRate
            ?? GetFirstInt32(additionalProperties, "elbruno.whisper.sample_rate", "sampleRate")
            ?? throw new ArgumentException(
                $"Raw audio requests require {nameof(SpeechToTextOptions.SpeechSampleRate)} or an 'sampleRate' additional property.",
                nameof(options));

        return new WhisperAudioFormat(
            sampleRate,
            channels ?? 1,
            ParseSampleFormat(sampleFormatText, mediaType));
    }

    private static WhisperAudioSampleFormat ParseSampleFormat(string? sampleFormatText, string? mediaType)
    {
        if (!string.IsNullOrWhiteSpace(sampleFormatText))
        {
            return sampleFormatText.Trim().ToLowerInvariant() switch
            {
                "pcm16" or "pcm-16" or "int16" or "s16le" => WhisperAudioSampleFormat.Pcm16,
                "float32" or "float" or "f32" => WhisperAudioSampleFormat.Float32,
                _ => throw new ArgumentException($"Unsupported Whisper sample format '{sampleFormatText}'.", nameof(sampleFormatText))
            };
        }

        if (!string.IsNullOrWhiteSpace(mediaType))
        {
            if (mediaType.Contains("float", StringComparison.OrdinalIgnoreCase) ||
                mediaType.Contains("f32", StringComparison.OrdinalIgnoreCase))
            {
                return WhisperAudioSampleFormat.Float32;
            }
        }

        return WhisperAudioSampleFormat.Pcm16;
    }

    private static bool ShouldTranslate(SpeechToTextOptions options, string? language, bool currentValue)
    {
        var targetLanguage = options.TextLanguage;
        if (string.IsNullOrWhiteSpace(targetLanguage))
        {
            return currentValue;
        }

        if (targetLanguage.Equals("en", StringComparison.OrdinalIgnoreCase))
        {
            return !string.Equals(language, "en", StringComparison.OrdinalIgnoreCase);
        }

        if (language is null || targetLanguage.Equals(language, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        throw new NotSupportedException(
            $"Whisper only supports translation to English. Requested output language '{targetLanguage}' is not supported.");
    }

    private static void ApplyAdditionalProperties(WhisperOptions whisperOptions, AdditionalPropertiesDictionary? additionalProperties)
    {
        if (additionalProperties is null)
        {
            return;
        }

        if (GetFirstBoolean(additionalProperties, "elbruno.whisper.enable_timestamps", "enableTimestamps") is bool enableTimestamps)
        {
            whisperOptions.EnableTimestamps = enableTimestamps;
        }

        if (GetFirstBoolean(additionalProperties, "elbruno.whisper.translate", "translate") is bool translate)
        {
            whisperOptions.Translate = translate;
        }

        if (GetFirstInt32(additionalProperties, "elbruno.whisper.max_tokens", "maxTokens") is int maxTokens)
        {
            whisperOptions.MaxTokens = maxTokens;
        }

        if (GetFirstSingle(additionalProperties, "elbruno.whisper.temperature", "temperature") is float temperature)
        {
            whisperOptions.Temperature = temperature;
        }
    }

    private static SpeechToTextResponse CreateResponse(TranscriptionResult result, string modelId)
    {
        var additionalProperties = new AdditionalPropertiesDictionary
        {
            [AudioDurationMetadataKey] = (long)result.Duration.TotalMilliseconds,
            [ModelIdMetadataKey] = modelId,
            [ExecutionProviderMetadataKey] = "onnxruntime"
        };

        if (!string.IsNullOrWhiteSpace(result.DetectedLanguage))
        {
            additionalProperties[DetectedLanguageMetadataKey] = result.DetectedLanguage;
        }

        if (result.Segments is not null)
        {
            additionalProperties[SegmentsMetadataKey] = result.Segments;
        }

        if (result.Words is not null)
        {
            additionalProperties[WordsMetadataKey] = result.Words;
        }

        return new SpeechToTextResponse(result.Text)
        {
            StartTime = TimeSpan.Zero,
            EndTime = result.Duration,
            ModelId = modelId,
            RawRepresentation = result,
            AdditionalProperties = additionalProperties
        };
    }

    private bool CanUseSharedClient(WhisperOptions requestOptions)
    {
        return string.Equals(requestOptions.Model.Id, _defaultOptions.Model.Id, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(requestOptions.Language, _defaultOptions.Language, StringComparison.OrdinalIgnoreCase) &&
               requestOptions.Translate == _defaultOptions.Translate &&
               requestOptions.MaxTokens == _defaultOptions.MaxTokens &&
               Math.Abs(requestOptions.Temperature - _defaultOptions.Temperature) < float.Epsilon &&
               requestOptions.EnableTimestamps == _defaultOptions.EnableTimestamps &&
               string.Equals(requestOptions.ModelPath, _defaultOptions.ModelPath, StringComparison.Ordinal) &&
               string.Equals(requestOptions.CacheDirectory, _defaultOptions.CacheDirectory, StringComparison.Ordinal) &&
               requestOptions.EnsureModelDownloaded == _defaultOptions.EnsureModelDownloaded &&
               requestOptions.Concurrency.MaximumConcurrentRequests == _defaultOptions.Concurrency.MaximumConcurrentRequests &&
               requestOptions.Concurrency.QueueTimeout == _defaultOptions.Concurrency.QueueTimeout &&
               requestOptions.Concurrency.EnableSessionPooling == _defaultOptions.Concurrency.EnableSessionPooling;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, typeof(WhisperSpeechToTextClient));
    }

    private static SpeechToTextClientMetadata CreateMetadata(WhisperOptions options)
    {
        return new SpeechToTextClientMetadata(ProviderName, ProviderUri, options.Model.Id);
    }

    private static WhisperOptions CloneWhisperOptions(WhisperOptions source)
    {
        return new WhisperOptions
        {
            Model = source.Model,
            ModelPath = source.ModelPath,
            CacheDirectory = source.CacheDirectory,
            EnsureModelDownloaded = source.EnsureModelDownloaded,
            Language = source.Language,
            Translate = source.Translate,
            MaxTokens = source.MaxTokens,
            Temperature = source.Temperature,
            EnableTimestamps = source.EnableTimestamps,
            Concurrency = new WhisperConcurrencyOptions
            {
                MaximumConcurrentRequests = source.Concurrency.MaximumConcurrentRequests,
                QueueTimeout = source.Concurrency.QueueTimeout,
                EnableSessionPooling = source.Concurrency.EnableSessionPooling
            }
        };
    }

    private static string? GetFirstString(AdditionalPropertiesDictionary? additionalProperties, params string[] keys)
    {
        return TryGetValue(additionalProperties, out var value, keys)
            ? ConvertToString(value)
            : null;
    }

    private static int? GetFirstInt32(AdditionalPropertiesDictionary? additionalProperties, params string[] keys)
    {
        return TryGetValue(additionalProperties, out var value, keys)
            ? ConvertToInt32(value)
            : null;
    }

    private static float? GetFirstSingle(AdditionalPropertiesDictionary? additionalProperties, params string[] keys)
    {
        return TryGetValue(additionalProperties, out var value, keys)
            ? ConvertToSingle(value)
            : null;
    }

    private static bool? GetFirstBoolean(AdditionalPropertiesDictionary? additionalProperties, params string[] keys)
    {
        return TryGetValue(additionalProperties, out var value, keys)
            ? ConvertToBoolean(value)
            : null;
    }

    private static bool TryGetValue(AdditionalPropertiesDictionary? additionalProperties, out object? value, params string[] keys)
    {
        value = null;
        if (additionalProperties is null)
        {
            return false;
        }

        foreach (var key in keys)
        {
            if (additionalProperties.TryGetValue(key, out value))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ConvertToString(object? value)
    {
        return value switch
        {
            null => null,
            string text => text,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
    }

    private static int? ConvertToInt32(object? value)
    {
        return value switch
        {
            null => null,
            int number => number,
            long number => checked((int)number),
            short number => number,
            byte number => number,
            double number => checked((int)number),
            float number => checked((int)number),
            decimal number => checked((int)number),
            string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => throw new ArgumentException($"Unable to convert '{value}' to an integer.")
        };
    }

    private static float? ConvertToSingle(object? value)
    {
        return value switch
        {
            null => null,
            float number => number,
            double number => (float)number,
            decimal number => (float)number,
            int number => number,
            long number => number,
            string text when float.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => throw new ArgumentException($"Unable to convert '{value}' to a floating-point value.")
        };
    }

    private static bool? ConvertToBoolean(object? value)
    {
        return value switch
        {
            null => null,
            bool boolean => boolean,
            string text when bool.TryParse(text, out var parsed) => parsed,
            int number => number != 0,
            long number => number != 0,
            _ => throw new ArgumentException($"Unable to convert '{value}' to a Boolean value.")
        };
    }
}
