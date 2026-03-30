# Decision: .NET Aspire Orchestration for BlazorWhisper

**By:** Ripley (Backend Dev)
**Date:** 2025-07-14
**Status:** Implemented

## Context

Bruno wanted better observability for the BlazorWhisper sample app after a WAV upload crash (caused by the ONNX empty cache tensor bug, now fixed). Aspire provides a dashboard with distributed tracing, logging, and health checks out of the box.

## Decision

Added .NET Aspire orchestration to the BlazorWhisper sample:

1. **BlazorWhisper.AppHost** — Aspire AppHost (Aspire.AppHost.Sdk/13.1.3, net10.0) that orchestrates the Blazor app
2. **BlazorWhisper.ServiceDefaults** — Shared project with OpenTelemetry, health checks, resilience, and service discovery
3. **BlazorWhisper upgraded to net10.0** — Required for ServiceDefaults compatibility (the library already supports net10.0)

## Key Details

- AppHost entry point is `AppHost.cs` (Aspire template convention), not `Program.cs`
- ServiceDefaults uses OpenTelemetry packages v1.14.0 for traces/metrics/logs
- Health endpoints (`/health`, `/alive`) only exposed in Development environment
- BlazorWhisper can still run standalone without the AppHost

## Consequences

- **Positive:** Full observability via Aspire dashboard (traces, logs, metrics, health) for debugging transcription issues
- **Positive:** Standard resilience and service discovery patterns ready if more services are added
- **Trade-off:** BlazorWhisper now targets net10.0 instead of net8.0 (acceptable since .NET 10 SDK is available)
