# Cascade.UI.GoldenText — Text Golden-Image Regression Suite (WP-3506)

Text rendering fails by *silent visual decay*: the `pow(alpha, 1.8)` shader
regression of 2026-06-10 shipped invisibly and was found by a human noticing
one malformed 'n' in a screenshot. This suite is the safety net that turns
every later text-calibration change (WP-3507–3510) into a reviewable diff
instead of a leap of faith.

## What it covers

Two layers, deliberately different in what they need to run:

**1. Golden-image comparisons (`GoldenTextTests`)** — 24 specimen pages
(Inter Regular/Medium/SemiBold × white-on-black/black-on-white × layout
scales 1.0/1.25/1.5/2.0) rendered by `tests/Cascade.UI.GoldenTextFixture`
through the **full Cascade pipeline**: `DrawContext.DrawText` → HarfBuzz
shaping → EtchBackend glyph ops → GPU glyph instances → presented swapchain
bytes. Goldens rendered through Etch alone would not have caught the 2026-06
bug class (it lived in the Cascade glyph shader and quad placement), so the
capture is the exact presented frame, read through the same buffer
`cascade mcp screenshot` reads.

Each page shows pangrams, digits, and the known regression strings
("Donut Gauges", "45%", "91%") at font sizes 8/11/13/16/24/40/72.

A companion test (`ForcedAtlasResetEveryFrame_MatchesGolden`) renders one page
with `CASCADE_FORCE_ATLAS_RESET=1`, which clears both glyph atlases at the
start of every frame so every glyph re-rasterizes into a fresh atlas. The
output must be byte-identical to the normal golden — proving a mid-session
glyph-atlas reset (WP-3509's churn-recovery mechanism) yields a visually
complete frame.

**2. Structural junction probes (`JunctionProbeTests`)** — headless,
raster-level, driver-proof. Single-stroke letters (n/m/h/u/o) at sizes
8–16 px across all three weights and all four subpixel buckets must stay one
connected component; three curated probes additionally assert the measured
junction *strength* of 'm' stem joins. Faces come from
`EtchBackend.GetOrCreateFontFace` — the production path, including the
production hinting default — so a hinting regression is caught even where no
golden can run. A permanent sensitivity test forces Normal hinting (what
faces fall back to if the production Slight override is removed) and asserts
at least one curated probe breaks: the probes prove their own teeth on every
run.

**3. Emoji / COLR color-glyph path (`EmojiPage_*`, WP-3520)** — the `emoji`
specimen page draws emoji that are absent from Inter and itemize to the system
color-emoji font (Segoe UI Emoji on Windows), exercising the COLR/CPAL color
atlas + color glyph shader end to end. These are **not pixel-golden'd**: emoji
artwork is system- and OS-version-dependent (the same redesign problem that made
WP-3518 decline native-DPI goldens), so a checked-in reference would be
machine-specific and would flake on any font update. Instead:
- `EmojiPage_RendersColorGlyphs` asserts the captured frame contains genuinely
  *chromatic* pixels (R/G/B not all equal) — a grayscale-only or tofu render has
  none, so the color path is proven without pinning exact artwork.
- `EmojiPage_SurvivesForcedColorAtlasReset` captures the page normally and again
  with `CASCADE_FORCE_ATLAS_RESET=1` and asserts the two frames are effectively
  identical — the color-atlas churn-recovery analog of
  `ForcedAtlasResetEveryFrame_MatchesGolden`, robust by self-comparison (both
  captures use the same font, so font drift is irrelevant).

Fallback *itemization* (a missing primary glyph resolving to the correct
substitute font with real, non-`.notdef` glyphs) is unit-tested driver-free in
`tests/Cascade.UI.Tests` (`EmojiColorAndFallbackTests`).

## Tolerance rule

A page passes when **at most 0.1 % of pixels exceed a per-channel delta of
4** and the mean per-channel error is ≤ 1.0. Tuned so driver noise on AA
edges cannot flake, while the historical gamma regression fails by ~90× the
budget (measured 2026-06-12: 8.9 % of pixels out of tolerance, mean error
3.2). Failures emit `bin/golden-failures/<page>-actual.png` and
`<page>-diff.png` (4-panel: actual | golden | raw diff | heatmap, via
Etch.Testing's PixelDiffPngWriter).

## DPI approximation (read before trusting a scale-factor result)

OS DPI cannot be forced per test on a real window, so the DPI axis is
approximated with **in-app layout scale factors** (1.0/1.25/1.5/2.0): the
specimen canvas scales by `pageScale / PixelRatio`, which neutralizes the
machine's actual DPI and makes one specimen unit map to exactly `pageScale`
device pixels on any machine. The capture region
(`sheet × pageScale` device pixels) is therefore machine-independent.
True fractional-DPI *window* behavior (WM_DPICHANGED, surface rescale,
reposition to the OS-suggested rect, PixelRatio propagation) is **not**
golden-tested here, and **WP-3518 decided it stays that way**: real per-test OS
DPI still cannot be forced on a headless/CI window — it is the actual monitor's
DPI, and `SetProcessDpiAwarenessContext` is process-level — so native 125%/150%
goldens would be machine-dependent and non-deterministic, exactly what the
in-app-scale approximation above exists to avoid. Instead the live window path
is covered deterministically by an integration test,
`DpiChangeTests.LiveDpiChange_ResizesSurface_AndScalesContentCoherently`
(tests/Cascade.UI.Integration): it injects a DPI change via
`cascade_set_render_param param=dpi` (the same in-process path a real
WM_DPICHANGED runs — the OS message can't be forged cross-process because its
lParam is a RECT pointer in the app's address space) and asserts the surface
resizes by the DPI ratio and content re-renders at the new scale (no stale
scale). **Division of labor:** these goldens own the rasterizer/placement math
at fixed PixelRatios; the integration test owns the window-rescale behavior.

## Known content gap (WP-3509)

The 72 px lines are omitted from scale-2.0 pages: the glyph atlas silently
drops glyphs taller than 127 px (its shelf rowHeight — Inter caps at
effective font size 144 rasterize to 140 px of ink). A golden must encode
correct rendering, not a known bug. When WP-3509 lands, remove the
`MaxEffectiveRasterSize` filter in the fixture's `PageSpec` and regenerate.

## Regenerating goldens

```powershell
$env:CASCADE_GOLDEN_REGEN = "1"
dotnet test tests/Cascade.UI.GoldenText
$env:CASCADE_GOLDEN_REGEN = $null
dotnet test tests/Cascade.UI.GoldenText   # verify the fresh goldens pass
```

Review the image changes (git diff of `Goldens/`) before committing —
regeneration makes the tests pass vacuously, so an unreviewed regen can
launder a regression into the baseline. Goldens were recorded 2026-06-12 on
a Windows machine with an NVIDIA GPU; another GPU/driver may differ on AA
edges within (or slightly beyond) tolerance — if a fresh machine fails
goldens with tiny deltas, inspect the heatmap before suspecting the code.

## CI decision (the WP-3506 caveat, resolved 2026-06-12)

**There is currently no functioning CI.** The repository has no git remote;
the GitHub workflow files under `.github/workflows` are aspirational and
would fail on any clean checkout anyway (the solution references the Etch
repo as a sibling `..\Etch` path). The "what does the GPU stack fall back to
on the CI runner" question is therefore unanswerable today and is recorded
here instead of silently guessed:

- **Junction probes** are the CI-able portion: headless, no GPU, no window,
  deterministic (measured 0 flakes over 20 consecutive runs). When CI
  becomes real, they gate every PR touching `Etch.Text` or
  `Cascade.UI.Backend.Etch` on any runner OS.
- **Golden comparisons** need a Windows machine with a working GPU presenter
  and a desktop session. They run locally (they are part of the normal
  `dotnet test` tree, so they cannot silently not-run — without a GPU they
  fail loudly with the reason). Whether a hosted runner's WARP adapter can
  serve them is to be determined when a remote/CI exists; until WP-3513/3514,
  CPU-fallback capture is not available as a substitute.

## Build note

The fixture window for scale 1.25 (800 px wide) exposed a latent crash:
`PerformCapture` used an unpadded `BytesPerRow`, and wgpu requires 256-byte
row alignment for texture→buffer copies — any window width not divisible by
64 panicked the present thread. Fixed in `EtchGpuPresenter` (padded staging
rows, de-strided on readback) as part of this WP.
