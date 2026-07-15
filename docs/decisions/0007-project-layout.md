# ADR 0007 — Two projects, layers as folders

**Status:** accepted · **Date:** 2026-07-14

## Context

The design has a real boundary: headless core logic (media, transcription, subtitles,
pipeline) on one side, Avalonia UI on the other. The question is how many `.csproj` files that
boundary needs.

## Decision

**Two projects**: `Rivet.Core` (headless) and `Rivet.App` (Avalonia). The finer layers —
`Media`, `Transcription`, `Whisper`, `Subtitles`, `Pipeline`, `Audio` — are **folders inside
Core**, not separate assemblies.

The boundary that actually matters is "Core must not depend on Avalonia, and must reach ffmpeg
and whisper.cpp only through interfaces". That is enforced by Core simply not referencing
Avalonia and by the composition root in `Rivet.App` being the one place concrete types are
named. Splitting Core into five assemblies would add project-reference bookkeeping to enforce
a boundary a single `using`-discipline rule already enforces.

## Consequences

- `ServiceRegistration` is the only file that names `FFmpegMediaProcessor`,
  `WhisperTranscriberFactory`, etc. Everything else takes `IMediaProcessor` / `ITranscriber`.
- Tests can reference `Rivet.Core` directly for the pipeline and go through a fake
  `IMediaProcessor`/`ITranscriber` without touching ffmpeg or a model.
- Same layout as Steno, for the same reason.
