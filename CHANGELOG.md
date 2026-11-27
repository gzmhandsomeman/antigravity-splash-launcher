# Changelog

All notable changes to this project will be documented in this file.

## [1.0.0] - 2026-11-27

### Added
- 15-second 6-act fullscreen Zero-G cinematic startup animation blending Apple Keynote volumetric lighting and Microsoft Fluent dark glassmorphism HUD panels.
- Automatic `512x512` official icon extraction from `app.asar` at build time (`build.ps1`) with multi-stage fallback and procedural prism icon (`IconAssetLoader.cs`).
- GPU-friendly `DrawingVisual` procedural renderer (`CinematicRenderer.cs`) featuring 3D perspective spacetime grid, 3D gyroscopic rings, polyhedral lattice, 16-trace silicon data bus, triple shockwaves, anamorphic lens flare, 100 zero-G particles, and 4 dark-glass HUD telemetry cards.
- 44.1 kHz 16-bit stereo procedural soundtrack synthesizer (`AudioSynthesizer.cs`) enabled via `--sound`.
- Interactive `--preview` mode (`Space` pause/resume, `←`/`→` scrub, `R` replay, `Esc` exit), `--fast` (3× speed), `--windowed`, `--target`, `--capture`, `--capture-gif`, and `--bench` CLI flags.
- Shortcut installer (`install-shortcut.ps1`) supporting Desktop, Taskbar, and Start Menu backup, installation, and restoration (`-RestoreDefault`).
- GitHub Actions CI build workflow (`.github/workflows/build.yml`) and bilingual documentation (`README.md`, `README_EN.md`).

### Performance
- Eliminated per-frame `dc.PushOpacity()` allocations via 16-step alpha-bucketed frozen brushes/pens, cached `FormattedText`, pre-baked floor reflection bitmaps, and removed per-frame shader effects — improving native 2560×1440 fullscreen performance from `4.0 FPS` to `47.5 FPS` (~12× speedup).
