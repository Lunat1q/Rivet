# ADR 0001 — Transcription engine: whisper.cpp, accessed via Whisper.net

**Status:** accepted · **Date:** 2026-07-14

## Context

The engine must be [ggml-org/whisper.cpp](https://github.com/ggml-org/whisper.cpp) — the
C/C++ ggml inference port. Not OpenAI's Python `whisper`, not `faster-whisper`/CTranslate2,
not a hosted API. Everything runs locally; the video never leaves the machine. That leaves
only *how a .NET process calls into it*.

## Options

1. **`Whisper.net` (NuGet)** — a managed binding whose native payload (`Whisper.net.Runtime`)
   **is whisper.cpp compiled from ggml-org/whisper.cpp**. It P/Invokes `whisper_full` etc.,
   loads the same `ggml-*.bin` models, and ships CPU / CUDA / Vulkan / CoreML runtimes.
2. **Hand-written P/Invoke** over a `whisper.dll` we build ourselves.
3. **Shell out to `whisper-cli.exe`** per clip.

## Decision

**Option 1.** Whisper.net is not a different engine — it is whisper.cpp plus the `DllImport`
layer we would otherwise write by hand, with native builds already produced for win-x64 + GPU
backends. This is the same binding Steno uses, for the same reasons.

Option 3 is viable for a pure batch tool (no per-utterance spawn cost, one clip = one process),
but still means a model *reload* per clip and parsing text output for timestamps; the binding
hands us structured `SegmentData` with times and probabilities directly.

Option 2 stays cheap to switch to: the codebase talks to `ITranscriber`, never to Whisper.net
types. `WhisperTranscriber` is the only file that would change.

## Consequences

- Models are GGML, from the whisper.cpp model set — the only format whisper.cpp accepts.
- We inherit whisper.cpp's input constraint: **16 kHz mono float32 PCM**. That is what
  `IMediaProcessor.ExtractAudioAsync` produces and the only audio format inside Core.
- Native runtime is chosen by NuGet package; see ADR 0004 for why Vulkan.
