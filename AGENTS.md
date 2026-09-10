# DLSS Swapper FG Preview

Personal GPLv3 fork of beeradmoore/dlss-swapper. Active branch: feature/frame-generation-preview. Keep upstream WinUI game/DLL workflow; add experimental FG management rather than rewriting UI.

- Targets: Black Myth Wukong and Wuchang Fallen Feathers; RTX 3080 and user CMP 40HX unlocked (SM75 route, not certified RTX 2060 SUPER equivalence). Gameplay and GPU performance remain unvalidated until user measurements.
- Native 0.2.3 backend pinned to sdli1995/dlssg_for_sm86 commit 1fb9ecbd980b1f191c092dce1b072b3cb9bd8984. Do not bundle vendor/model DLLs or silently update the pin. Public backend currently lacks buildable source; licenses need clarification before redistribution.
- 2x/4x UI is a maximum generated-frame count, NOT proof of active multiplier. Do not claim unlocking based on copying files or finding logs. No automatic game graphics settings are changed in preview 0.1.
- FgInstaller owns only journaled proxy/INI; preserve other mods, external edits and original INI. Journal before writes. Do not bypass checks, modify drivers, spoof GPU identity, or disable protections.
- Core fixture tests: dotnet run --project tests/FrameGeneration/FrameGeneration.Tests.csproj. Complete WinUI builds on Windows using .github/workflows/fg-preview.yml. macOS core tests cannot certify UI or gameplay.
- Portable settings isolated in StoredData-FG-Preview; application updater points to this fork.
- README.md is the Chinese user guide; upstream README kept at docs/README.upstream.md.

## Verification recorded 2026-09-10

Source commit 9f83d04: Windows x64 WinUI publish succeeded in Actions run 34445794938; 26 core fixtures passed on Windows and macOS. Optional --package-smoke adds 3 passing local checks for actual pinned DLL download/install/restore without executing it. Windows UI/gameplay remains pending user tests. Project status synchronized to the accessible Obsidian vault project page DLSS Swapper FG.

Upstream signing/winget workflows are restricted to the upstream repository; fork previews use fg-preview.yml and must not publish under the official winget package identity.
