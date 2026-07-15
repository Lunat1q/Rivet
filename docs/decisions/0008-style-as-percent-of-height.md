# ADR 0008 — Caption sizes are a percentage of video height

**Status:** accepted · **Date:** 2026-07-14

## Context

`SubtitleStyle` is meant to be saved and reused across clips. But clips are not all the same
resolution — a phone export is 1080×1920, a downscaled one is 720×1280, an upscale is 4K. A
font size in **pixels** means a saved preset looks tiny on one and huge on another.

## Decision

Store every size that must scale with the frame as a **percentage of video height**: font
size, outline width, vertical margin. `AssSubtitleWriter` converts them to pixels against the
actual `VideoInfo.Height` when it writes the file (font `7%` of 1920 → 134 px). Horizontal
margins key off width. Colours, font family, word count, uppercase and position are
resolution-independent and stored as-is.

## Consequences

- One preset produces the same visual weight on any export resolution.
- The ASS canvas is set to the real video dimensions (`PlayResX/Y`) so libass scales nothing
  unexpectedly.
- The UI expresses size as a percent slider ("7.0% of height"), which is also how it reads
  back — no hidden pixel math for the user.
