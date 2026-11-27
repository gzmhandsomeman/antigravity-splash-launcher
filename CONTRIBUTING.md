# Contributing to Antigravity Zero-G Splash Launcher

Thank you for your interest in contributing!

## Development Rules

1. **Strict C# 5 Syntax Compatibility**:
   This project compiles directly with the built-in Windows `.NET Framework 4.x` compiler (`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`) with zero external NuGet packages or SDKs.
   - Do **not** use C# 6+ syntax (`$"..."` string interpolation, `?.`, `=>` expression-bodied members, `nameof`, `out var`, tuples, etc.).
   - Always test building via:
     ```powershell
     powershell -ExecutionPolicy Bypass -File .\build.ps1
     ```
2. **Preserve Zero External Dependencies**:
   Only reference standard `.NET Framework 4.x` / WPF assemblies already included in `build.ps1`.
3. **Avoid Hardcoded Personal Paths**:
   Use environment variables (`$env:LOCALAPPDATA`, `Environment.SpecialFolder.LocalApplicationData`, etc.) and CLI flags (`--target`) instead of machine-specific user paths.
4. **Verify Visuals & Performance**:
   - Use `.\AntigravityLauncher.exe --preview --sound` to inspect transitions across all 6 acts.
   - Use `.\AntigravityLauncher.exe --bench bench.log` to ensure fullscreen FPS stays smooth on high-DPI displays.
   - Use `.\AntigravityLauncher.exe --capture .\capture_out` to inspect keyframe PNGs.

## Pull Request Process

1. Fork the repository and create a feature branch (`git checkout -b feat/my-feature`).
2. Keep changes focused and cleanly formatted.
3. Update `README.md` / `README_EN.md` / `CHANGELOG.md` if adding user-facing flags or behaviors.
4. Ensure GitHub Actions Windows build passes cleanly.
