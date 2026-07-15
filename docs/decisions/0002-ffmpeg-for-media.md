# ADR 0002 — Media handling: shell out to ffmpeg/ffprobe, do not bundle

**Status:** accepted · **Date:** 2026-07-14

## Context

Rivet has to do three things to a video container: read its shape, pull its audio out as
whisper-ready PCM, and burn finished subtitles back in. All three are ffmpeg's day job.

## Options

1. **Invoke `ffmpeg`/`ffprobe` as child processes** and pipe data over stdout.
2. **A managed wrapper** (FFMpegCore, Xabe.FFmpeg) that shells out for us.
3. **A native binding** (ffmpeg.autogen) — P/Invoke straight into libav*.

## Decision

**Option 1**, behind `IMediaProcessor`. The three operations are three short command lines;
audio comes back as raw `f32le` on stdout (no temp WAV), and progress comes from ffmpeg's own
`-progress` stream. A wrapper library (option 2) would be a dependency we own forever to save a
`ProcessStartInfo`, and option 3's binding tracks ffmpeg's ABI and turns a bad input into a
segfault instead of a non-zero exit code.

**We do not bundle the binaries.** ffmpeg is ~120 MB, its build/licence choice belongs to the
user, and any machine editing short-form video already has a copy. `FFmpegLocator` checks the
obvious places (`%LOCALAPPDATA%/Rivet/ffmpeg`, `C:\ffmpeg`) and otherwise trusts PATH; a
missing binary surfaces as one clear "install ffmpeg" error, not a mystery crash.

## Consequences

- ffmpeg must be present. That is the one external requirement, stated up front.
- The subtitles filter's path parser treats `:` and `\` as syntax. Rather than escape Windows
  paths, `BurnSubtitlesAsync` passes the `.ass` by **filename only** with ffmpeg's working
  directory set to its folder — no drive letter, no escaping (see `FFmpegMediaProcessor`).
- Burning re-encodes the video (`libx264`) and copies audio. A future "quality/CRF" knob lands
  here without touching anything above `IMediaProcessor`.
