# Blazor Components

`ElBruno.Whisper.BlazorComponents` is a Razor Class Library for building speech-to-text UIs with `ElBruno.Whisper`.

## Install

```bash
dotnet add package ElBruno.Whisper.BlazorComponents
```

## Register services

```csharp
using ElBruno.Whisper;
using ElBruno.Whisper.BlazorComponents.Extensions;

builder.Services.AddWhisper(options =>
{
    options.Model = KnownWhisperModels.WhisperTinyEn;
});

builder.Services.AddWhisperBlazorComponents(options =>
{
    options.MaxHistoryItems = 50;
});
```

## Components

## `WhisperModelSelector`

Shows available models and their state (downloaded / not downloaded / downloading).

```razor
<WhisperModelSelector ModelManager="@modelManager"
                      OnModelSelected="OnModelSelected" />
```

- `ModelManager` (`IWhisperModelManager`) — required.
- `OnModelSelected` (`EventCallback<WhisperModelInfo>`) — callback on selection changes.

## `FileTranscriptionPanel`

File picker for WAV/MP3/OGG/FLAC with transcription execution and copy-to-clipboard output.

```razor
<FileTranscriptionPanel WhisperService="@whisperService"
                        SelectedModelId="@selectedModelId"
                        OnTranscribed="OnTranscribed" />
```

- `WhisperService` (`IWhisperService`) — required.
- `SelectedModelId` (`string?`) — selected model id from `WhisperModelSelector`.
- `OnTranscribed` (`EventCallback<TranscriptionResult>`) — callback when transcription completes.

## `LiveTranscriptViewer`

Displays interim text and final transcript updates from `IWhisperService.TranscriptUpdated`.

```razor
<LiveTranscriptViewer WhisperService="@whisperService"
                      ShowTimestamps="true"
                      ShowLanguageBadge="true" />
```

- `WhisperService` (`IWhisperService`) — required.
- `ShowTimestamps` (`bool`) — include per-segment timestamps.
- `ShowLanguageBadge` (`bool`) — show current detected language badge.

## `TranscriptionHistoryList`

Collapsible history of previous transcriptions with clear/copy actions.

```razor
<TranscriptionHistoryList MaxItems="20"
                          ShowClearAll="true" />
```

- `MaxItems` (`int`) — maximum items to show.
- `ShowClearAll` (`bool`) — show/hide clear-all button.

## Services in the package

- `IWhisperService` / `WhisperService`
- `IWhisperModelManager` / `WhisperModelManager`
- `ITranscriptionHistoryStore` / `InMemoryTranscriptionHistoryStore`

## Sample

See `src/samples/BlazorWhisper/Components/Pages/BlazorComponentsDemo.razor` and [Blazor Components Sample](blazor-components-sample.md).
