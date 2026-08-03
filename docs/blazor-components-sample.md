# Blazor Components Sample

The sample page for the reusable components is:

- `src/samples/BlazorWhisper/Components/Pages/BlazorComponentsDemo.razor`

## What it demonstrates

1. `WhisperModelSelector` to pick/download a model.
2. `FileTranscriptionPanel` to upload and transcribe audio files.
3. `LiveTranscriptViewer` to visualize transcript updates.
4. `TranscriptionHistoryList` to browse past transcriptions.

## Run the sample

```bash
cd src/samples/BlazorWhisper
dotnet run
```

Open `/blazor-components` in the running app.

## Notes

- The first transcription for a model may trigger model download.
- Model cache defaults to `%LOCALAPPDATA%/ElBruno/Whisper/models`.
- Uploaded files are copied to temporary files during processing.
