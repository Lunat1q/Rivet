# ADR 0010 — Transcribe, edit, then render: a two-phase pipeline

**Status:** accepted · **Date:** 2026-07-14

## Context

whisper is very good but not perfect: it mishears the odd word, and its per-word timing can be
loose (ADR 0003). The first version rendered in one shot — transcribe straight into a burned
video — so a single wrong word or a slightly-off highlight meant re-running the whole thing with
no way to fix it. For a tool whose entire output is on-screen text, "you get what whisper gave
you" is not good enough.

## Decision

Split the pipeline into two user-driven phases with an editing step between them:

1. **Transcribe** — `SubtitlePipeline.TranscribeAsync` returns a word-timed `Transcript` (and the
   video's dimensions). Nothing is burned.
2. **Edit** (optional) — the UI shows every word with its start/end in seconds. The user can fix
   the text and nudge the timings to realign the highlight, watching a **live preview frame**
   (`IMediaProcessor.RenderPreviewFrameAsync`) update as they scrub or restyle.
3. **Render** — `SubtitlePipeline.RenderAsync` takes the (possibly edited) transcript and burns it.

Editing is **optional**: once transcribed you can render immediately. `RunAsync` still chains both
phases for headless/one-shot use.

## Why a single-frame preview, not a video player

A true playing preview needs an ASS renderer inside the app (libass bindings, a decode/composite
loop) — a large amount of machinery to approximate what the final ffmpeg burn already does
correctly. Rendering **one frame** at the scrub position with the real ffmpeg+libass path is a
few hundred milliseconds, reuses the exact renderer the final output uses (so the preview cannot
lie), and is more than enough to judge text, style and timing. The final render is the only full
pass.

## Consequences

- `AssSubtitleWriter` and caption layout stay pure and are shared by preview and final render via
  `SubtitlePipeline.WriteAssAsync` — the preview is guaranteed to match the burn.
- Timings are edited in **seconds**, the unit a person scrubbing a short clip thinks in, not ASS
  centiseconds.
- Word-level realignment covers the residual cases ADR 0003 leaves (collapsed timestamps) without
  a heavier alignment model — the human is the alignment model of last resort.
