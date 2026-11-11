# SlideDevPresenter Agent Specification

This document describes the implemented scope, architecture, and operating constraints of the current repository. See [README.md](../README.md) for user-facing documentation and [Copilot instructions](../.github/copilot-instructions.md) for coding conventions and platform-specific implementation guidance.

## Scope

### Implemented

- Desktop shell and launcher around Slidev with a ribbon-style UI (Home, Library, View, Presentation, Browser, Edit tabs), library sidebar, and status bar
- Source management for local roots, local projects, hosted URLs, Git repositories, and GitHub repositories, including branch selection, checkout reuse, and library import/export (JSON)
- Git workflows with terminal-based cloning, confirmed pull/branch-switch operations, noninteractive fetch on repository refresh, and recovery guidance without discarding local changes; refresh is blocked while building or presenting
- Slidev build pipeline: dependency installation based on lockfile/`packageManager` (npm, pnpm, Yarn, Bun), manifest hashing to skip redundant installs, and local `@slidev/cli` build before launch; Stop cancels installation/build, and hosted URLs bypass compilation
- Slidev process lifecycle management (dev server start/stop/retry, actual port and loopback URL detection from ANSI-stripped current and legacy ready banners)
- Presenter and participant workflows: embedded WebView surfaces, swap between surfaces, participant window on a second display with display detection/selection, blank overlay mirroring, and keyboard routing via WebView messages
- Thumbnail, agenda, and timer panels alongside the presentation surface
- Embedded browser tabs for links opened from a deck, with external browser fallback
- Slide deck metadata reading (title, slide list, summaries) and PDF export via Slidev
- Editing integration with VS Code (Stable/Insiders) for local and checked-out projects
- Settings window (sources, editor, navigation shortcuts, theme) persisted as JSON
- Native macOS menu bar (app, File, Presentation, View, Window, Help) that mirrors ribbon actions with ⌘ shortcuts; Windows and Linux use the in-window ribbon only
- About window with application version and user-initiated in-app updates for installed Windows and macOS releases
- Self-contained Windows x64 and macOS Intel/Apple Silicon installers, GitHub Release packaging, automatic Homebrew tap publication, and Chocolatey release assets
- Tests for Core/Infrastructure services, app services, ViewModel command logic, and selected Avalonia views

### Not in scope (currently)

- Hosting Slidev as a static build instead of a dev server
- Collaboration or remote-control features beyond Slidev's own presenter sync
- Linux installers or in-app updates (Linux remains a build/test target)
- Unattended update downloads or restarts
- Publishing packages to the Chocolatey community feed

## Architecture

All projects target .NET 10. The desktop app uses Avalonia 12, compiled bindings, CommunityToolkit.Mvvm, Microsoft.Extensions.Logging, and Velopack.

- [Core](../src/SlideDevPresenter.Core): Domain models, service contracts, and shared source-list serialization/validation helpers; no UI or third-party dependencies
- [Infrastructure](../src/SlideDevPresenter.Infrastructure): Service implementations (Git CLI, Slidev build/host, metadata reader, PDF export, settings persistence, source scanning, editor discovery/launch); depends only on Core and logging abstractions
- [App](../src/SlideDevPresenter.App): Avalonia shell, views, ViewModels, platform services (display selection, presentation windows, WebView bridges, shortcuts, theme, native menus), and the Velopack update service
- [Tests](../tests/SlideDevPresenter.Tests): xUnit unit and service-level tests with hand-written fakes, plus focused view/layout checks

Services are constructed manually, primarily in [App.axaml.cs](../src/SlideDevPresenter.App/App.axaml.cs); there is no DI container. Keep Avalonia and Velopack integration in the App project.

## Distribution and Updates

- The [desktop release workflow](../.github/workflows/release.yml) accepts `vMAJOR.MINOR.PATCH` tags with optional SemVer pre-release suffixes. It builds and tests the solution, then packages self-contained Windows x64 and macOS x64/arm64 applications with Velopack. The tag supplies the application and package version.
- Installers include the .NET runtime; the Windows installer also checks for WebView2. Node.js and the project's package manager remain external prerequisites for local decks, and Git is needed for repository sources. Settings and checkouts live outside the installation and survive updates.
- A draft GitHub Release is created only after all three platform packages succeed. Pre-release tags mark the draft as a pre-release. Reruns may replace draft assets, but published releases receive only missing assets; existing published assets are preserved.
- Both stable and pre-release builds include a Homebrew cask and Chocolatey `.nupkg` with installer SHA-256 checksums. The [Homebrew publication workflow](../.github/workflows/publish-homebrew-cask.yml) synchronizes the [tap](https://github.com/norschel/homebrew-slidedevpresenter) only after packaging succeeds and the release is public and stable. Packaging completion and release publication/edits trigger this check, including promotion from pre-release to stable. Drafts and pre-releases never update the tap.
- Preserve the `SlideDevPresenter` package ID and `win-x64`, `osx-x64`, and `osx-arm64` update channels. Keep all generated update feeds (`releases.*.json`) and full Velopack `.nupkg` assets attached to releases. Packaging uses full packages, not deltas.
- Windows signing and macOS signing/notarization are optional, secret-driven workflow steps. Without signing configuration, installers are unsigned; OS security checks are not bypassed. See [Publishing a Release](../README.md#publishing-a-release) for signing and tap credentials.
- In About, checking, downloading, and restarting to install are separate user actions. Restarting stops the presentation and saves settings before scheduling the update and exiting. Updates are not automatically applied at startup.
- The updater follows the installed platform/architecture and offers published stable releases by default. Opting into pre-releases applies only to the current About window; changing that choice requires a fresh check, even after a download. Unpackaged development builds and Linux do not enable in-app updates.

## Principles

- Reuse Slidev and the Slidev presenter view rather than reimplementing presentation features
- Keep UI and business logic separated; ViewModels delegate I/O, process, and Git work to services
- Use JSON-based configuration that stays backwards compatible with existing settings files. Shared library lists omit checkout paths and retain the legacy `github` type as a hosted URL; GitHub repository sources use `github-repository`
- Keep services testable without network, Git, processes, or the real settings location
- Any app-side behaviour layered on top of Slidev (for example the blank overlay) must be mirrored explicitly between presenter and participant surfaces
- Preserve native WebView keyboard, focus, visibility, and fullscreen handling across platforms; WebView focus bypasses Avalonia key events, so the web-message bridge remains necessary
- Treat local/repository decks as trusted code: dependency installation and compilation can execute project code. Use external Git credential tooling and surface failures with recovery guidance rather than resetting or discarding local changes

## Build and Verification

Run from the repository root with the .NET 10 SDK:

```sh
dotnet build ./SlideDevPresenter.sln
dotnet test ./SlideDevPresenter.sln
dotnet run --project ./src/SlideDevPresenter.App/SlideDevPresenter.App.csproj
```

- [Verify Build](../.github/workflows/verify-build.yml) restores, builds, and tests in Release configuration on Ubuntu, Windows, and macOS for pushes and pull requests
- [CodeQL](../.github/workflows/codeql.yml) analyzes C# and GitHub Actions on main-branch pushes/pull requests and a weekly schedule
- Builds and unit tests do not replace native-platform verification: test installation, launch, embedded browser behavior, uninstall, and an update from a previous version on each packaged platform before publishing a release
