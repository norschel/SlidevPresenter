# SlideDevPresenter Agent Specification

This document tracks the implementation direction for SlideDevPresenter. The MVP scope below is implemented; see `README.md` for user-facing documentation and `.github/copilot-instructions.md` for coding conventions.

## Scope

### Implemented

- Desktop shell and launcher around Slidev with a ribbon-style UI (Home, Library, View, Presentation, Browser, Edit tabs), library sidebar, and status bar
- Source management for local roots, local projects, hosted URLs, Git repositories, and GitHub repositories, including branch selection, checkout reuse, fetch/pull on refresh, and library import/export (JSON)
- Slidev build pipeline: dependency installation based on lockfile/`packageManager` (npm, pnpm, Yarn, Bun), manifest hashing to skip redundant installs, and `@slidev/cli` build before launch
- Slidev process lifecycle management (dev server start/stop/retry, port and URL detection)
- Presenter and participant workflows: embedded WebView surfaces, swap between surfaces, participant window on a second display with display detection/selection, blank overlay mirroring, and keyboard routing via WebView messages
- Thumbnail, agenda, and timer panels alongside the presentation surface
- Embedded browser tabs for links opened from a deck, with external browser fallback
- Slide deck metadata reading (title, slide list, summaries) and PDF export via Slidev
- Editing integration with VS Code (Stable/Insiders) for local and checked-out projects
- Settings window (sources, editor, navigation shortcuts, theme) persisted as JSON
- Unit tests for Core/Infrastructure services and ViewModel command logic

### Not in scope (currently)

- Hosting Slidev as a static build instead of a dev server
- Collaboration or remote-control features beyond Slidev's own presenter sync
- Package publishing or installer pipelines

## Architecture

- `src/SlideDevPresenter.Core`: Domain models (`Models/`) and service contracts (`Services/I*.cs`); no UI or third-party dependencies
- `src/SlideDevPresenter.Infrastructure`: Service implementations (Git CLI, Slidev build/host, metadata reader, PDF export, settings persistence, source scanning); depends only on Core and logging abstractions
- `src/SlideDevPresenter.App`: Avalonia 12 shell, views, CommunityToolkit.Mvvm ViewModels, and Avalonia-specific services (display selection, presentation windows, WebView bridges, shortcuts, theme)
- `tests/SlideDevPresenter.Tests`: xUnit unit and service-level tests with hand-written fakes

Services are wired manually in `App.axaml.cs`; there is no DI container.

## Principles

- Reuse Slidev and the Slidev presenter view rather than reimplementing presentation features
- Keep UI and business logic separated; ViewModels delegate I/O, process, and Git work to services
- Use JSON-based configuration that stays backwards compatible with existing settings files
- Keep services testable without network, Git, processes, or the real settings location
- Any app-side behaviour layered on top of Slidev (for example the blank overlay) must be mirrored explicitly between presenter and participant surfaces
