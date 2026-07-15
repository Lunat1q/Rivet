# ADR 0004 — Vulkan GPU backend; CPU/GPU is a per-model-load flag

**Status:** accepted · **Date:** 2026-07-14

## Context

The whisper backend is the single biggest factor in how long a render takes: on a discrete GPU
via Vulkan, whisper.cpp runs faster than real time; on a CPU it is many times slower. A
2-minute clip is the difference between a quick export and a coffee break.

## Decision

**Load the Vulkan runtime, ordered before CPU** (`WhisperRuntime.Configure`), deliberately
*not* Whisper.net's default order which tries CUDA first. The CUDA build ships no kernels for
the newest cards and takes the whole process down rather than throwing something catchable.
Vulkan covers NVIDIA + AMD + Intel from one package and degrades to CPU when there is no GPU.

**CPU vs GPU is a per-model-load flag** (`WhisperFactoryOptions.UseGpu`), not a library
choice — the Vulkan build carries the CPU kernels too, and a native library loads once per
process. So the user's Automatic/GPU/CPU choice takes effect without restarting the app, and
only forces a reload when it or the model actually changes.

Choosing **GPU** explicitly and finding none is a hard error, not a silent fall back to a CPU
that is 60× slower — that is a different experience, so we say so.

## Consequences

- One extra NuGet package (`Whisper.net.Runtime.Vulkan`) and no CUDA/driver install for users.
- Unlike Steno, Rivet does not ship a benchmark button (yet): offline, the cost of guessing
  wrong is a slower export, not a broken live call. `ITranscriptionBackend` still reports which
  backend won so the UI can show it. A "test speed" button is a clean future add.
