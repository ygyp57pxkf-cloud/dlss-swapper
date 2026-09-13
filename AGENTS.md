# DLSS Swapper FG Preview

Personal GPLv3 fork of beeradmoore/dlss-swapper. Active branch: feature/frame-generation-preview. Keep upstream WinUI game/DLL workflow; add experimental FG management rather than rewriting UI.

- Targets: Black Myth Wukong and Wuchang Fallen Feathers; RTX 3080 and user CMP 40HX unlocked (SM75 route, not certified RTX 2060 SUPER equivalence). Gameplay and GPU performance remain unvalidated until user measurements.
- Native 0.2.3 remains the default at 1fb9ecbd980b1f191c092dce1b072b3cb9bd8984; user-authorized optional Native 0.2.4 is pinned at 5f62ff44a9c08f9841fa605e7b7160f79ccd2c40. Both are sdli1995/dlssg_for_sm86. Do not bundle vendor/model DLLs or silently change these pins. Public backend currently lacks buildable source; licenses need clarification before redistribution.
- 2x/3x/4x UI is a maximum generated-frame count, NOT proof of active multiplier. Do not claim unlocking based on copying files or finding logs. No automatic game graphics settings are changed in preview 0.1.
- FgInstaller owns only journaled proxy/INI; preserve other mods, external edits and original INI. Journal before writes. Do not bypass checks, modify drivers, spoof GPU identity, or disable protections.
- Core fixture tests: dotnet run --project tests/FrameGeneration/FrameGeneration.Tests.csproj. Complete WinUI builds on Windows using .github/workflows/fg-preview.yml. macOS core tests cannot certify UI or gameplay.
- Portable settings isolated in StoredData-FG-Preview; application updater points to this fork.
- README.md is the Chinese user guide; upstream README kept at docs/README.upstream.md.

## Preview 0.3 tool integration (2026-09-13)

- EnhancementToolsPage is a lazy navigation page. Never download, hash large optional tools, or initialize them during game-library startup. Hashing/extraction runs on worker tasks; keep the original scan fix intact.
- ToolCatalog pins DLSS5-Swapper 2.2.7 and DLSS 5 Visual Enhancer 8.0 by release URL, byte size and SHA-256. Download/import only on user click, stage under StoredData-FG-Preview/EnhancementTools, launch only on a separate click. Do not bundle third-party tools or model/runtime assets in our ZIP.
- This version opens each external tool's existing UI. No game-path CLI is confirmed: copy the selected path, then the user uses Add a game. GameTargetInspector only reads PE architecture/static imports and same-directory Mod names; unknown runtime APIs remain unknown. Never claim NR is active from a ready receipt or successful process launch.
- Existing tool directories can contain user media outputs/configuration: never overwrite incomplete installs or remove their directory as a cache cleanup. Only owned download cache/temporary staging is disposable. Restore game changes through the manager that installed them, not by deleting the external tool.
- Visual Enhancer 8.0 marks AI compatibility from an RTX substring in the GPU name. CMP 40HX may be marked incompatible despite its separate NVENC unlock record; do not spoof names or change drivers to bypass this. Recommend RTX 3080 first. GPU, Windows UI and actual game/media results await user tests.
- Tests: dotnet run --project tests/EnhancementTools/EnhancementTools.Tests.csproj (40 fixture checks); add -- --packages <directory> for four actual pinned package preparation checks without execution. All 44 passed on macOS; Windows CI must also pass before publication. Existing FrameGeneration tests remain required.
- docs/Enhancement-Tools.md is the Chinese tool guide. fg-preview.yml workflow_dispatch can publish fg-preview-0.3 only when publish_preview=true and checks/build/package verification succeed. Keep published tags immutable.

Source 3cf9719 passed all 84 core fixture checks on Windows (44 FG + 40 tools) and the Windows x64 portable build/package checks in Actions run 34749573457. That runner published fg-preview-0.3. Four additional actual-package preparation checks passed locally without executing third-party programs. Real Windows UI, GPU, game and media acceptance remains pending; the accessible Obsidian project page tracks user testing.

## Verification recorded 2026-09-10

Source commit 9f83d04: Windows x64 WinUI publish succeeded in Actions run 34445794938; 26 core fixtures passed on Windows and macOS. Optional --package-smoke adds 3 passing local checks for actual pinned DLL download/install/restore without executing it. Windows UI/gameplay remains pending user tests. Project status synchronized to the accessible Obsidian vault project page DLSS Swapper FG.

Upstream signing/winget workflows are restricted to the upstream repository; fork previews use fg-preview.yml and must not publish under the official winget package identity.

## Preview 0.2 (2026-09-13)

Includes the pre-existing local library scanning fix after review. Version switching must preserve the initial INI backup and record previous DLL hashes so an interrupted upgrade can still be restored. Log summaries describe observed events only; never equate a copied DLL, an old log, or a generated_count event with a full gameplay/quality pass. The separate CMP40HX-Unlock repository records 2026-09-13 machine-specific driver/Gen2/NVENC success; FG on that machine remains unverified here.

Validation for source 2ce04a5: 44 core checks passed on macOS and Windows; Windows portable build succeeded in Actions run 34740732863. Real backend download/install/upgrade/downgrade/restore was checked locally without executing DLLs. FG Preview 0.2 ZIP has an x64 app, guide and build metadata, and no FG backend DLLs; gameplay/UI acceptance remains pending.
