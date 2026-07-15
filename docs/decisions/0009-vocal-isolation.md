# ADR 0009 — Optional vocal isolation (Demucs), off by default

**Status:** accepted · **Date:** 2026-07-14

## Context

whisper transcribes worse when speech competes with music or heavy ambience — it drops words
or invents them. karaoke-forever (whose Russian pipeline prompted this) isolates vocals with
Spleeter before its whisper pass for exactly this reason. Rivet's captions are only as good as
the transcript, so the same lever helps on musical or noisy clips.

## Decision

Add an **optional** vocal-isolation pre-pass behind `IVocalIsolator`, implemented with **Demucs**
(`--two-stems=vocals`). When the user turns it on, the pipeline runs the audio through Demucs and
hands whisper the isolated `vocals` track; the video itself is never touched. Default is **off**.

- **Demucs, not Spleeter.** Spleeter is effectively unmaintained (old TensorFlow); Demucs is the
  current state of the art and actively developed. Two-stem mode (vocals vs. the rest) is ~2×
  faster than full 4-stem separation and all we need.
- **Optional and off by default.** Isolation is slow (minutes on CPU) and pointless on clean
  speech — which is most short-form talking-head content. It earns its cost only on music beds.
- **Not bundled.** Demucs drags in PyTorch (gigabytes). Like ffmpeg (ADR 0002) it is located, not
  shipped; turning the option on without it installed produces one clear "install Demucs" error.

## Alternatives

- **stable-ts / faster-whisper forced alignment** (karaoke-forever's actual Russian path). That
  aligns *known lyrics* to audio — it needs the text up front. Rivet transcribes from nothing, so
  there is no text to align; it doesn't apply. And it is a Python stack, at odds with the
  Whisper.net-native design (ADR 0001).
- **A denoise filter in ffmpeg** (`afftdn`, `arnndn`). Cheap, but removes hiss, not a music bed;
  it does not separate a voice from a song. Demucs does.

## Consequences

- One more optional external tool. The seam (`IVocalIsolator`) means swapping Demucs for Spleeter,
  a hosted service, or a future native separator is a one-file change.
- Temp `vocals` files are written under the system temp dir and cleaned by the OS; the isolated
  audio is only an input to whisper, never surfaced.
