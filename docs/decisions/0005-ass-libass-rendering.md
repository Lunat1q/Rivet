# ADR 0005 — Burn captions as ASS via libass; one Dialogue event per word

**Status:** accepted · **Date:** 2026-07-14

## Context

The captioned video is the product, so rendering has to be both correct (legible over any
footage) and produce the specific short-form look: big outlined text with the word being
spoken *right now* popping in a highlight colour.

## Options for rendering

1. **Write an ASS subtitle file; burn it with ffmpeg's `subtitles` filter (libass).**
2. **Rasterise every frame ourselves** — draw text onto bitmaps, composite, feed to ffmpeg.
3. **Overlay a transparent caption video** built separately.

## Decision

**Option 1.** libass already does outline, shadow, positioning, per-character layout, scaling
and wrapping correctly — reimplementing that (option 2) is weeks of work to render text worse
than a mature library does, and ffmpeg is already re-encoding the video anyway. Every
"hardcoded captions" tool of note takes this path because it is the one that renders ASS
styling faithfully.

## The highlight: per-word events, not karaoke

ASS has a karaoke tag (`\k`) that fills a line word-by-word on a timer. It is the obvious tool
and it is **rejected**: `\k` can only cross-fade a colour *fill*; it cannot grow the active
word or give it its own outline, which is the difference between "subtitles" and "TikTok
captions".

Instead, `AssSubtitleWriter` emits **one `Dialogue` event per word**. Each shows the whole
caption but wraps the active word in a full override block — highlight colour + scale-up
(`{\fscx115\fscy115\c&H..&}WORD{\r}`) — held until the next word starts, so there is never a
dead frame. The cost is one event per word (thousands for a long clip), which libass renders
without noticing.

## Timing: cards tile, never overlap

Emitting one event per word created two failure modes when whisper's word times are ragged
(it sometimes collapses several words onto one timestamp): events with equal times **overlap**,
so libass stacks two copies of the card on screen at once; and a card that ends at its last
word's timestamp **flashes** for a fraction of a second on fast speech.

Both are fixed by treating a card as a tiling problem, not a per-word one. Each card stays up
until the *next* card starts (capped at a 1 s linger into a silent gap), and its span is
partitioned into strictly-increasing, non-overlapping, minimum-150 ms slices — one per word,
placed at the word's start time but clamped to keep the sequence monotonic. Collapsed timestamps
get spread evenly instead of stacking. See `AssSubtitleWriter.WriteCaption`.

## Consequences

- Colours in `SubtitleStyle` are `#RRGGBB`; the writer converts to ASS's BGR `&H00BBGGRR`.
- Verified end to end: a hand-built transcript renders the active word green-and-enlarged with
  a black outline, centred on a 1080×1920 frame — the exact target look.
- Animated entrances (pop/slide-in) are a future add: more override tags in the same writer,
  no architectural change.
