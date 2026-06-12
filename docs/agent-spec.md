# SlideDevPresenter Agent Specification

This document tracks the implementation direction for SlideDevPresenter.

## Scope (MVP)

- Desktop shell and launcher around Slidev
- Source management for local roots, local projects, and hosted URLs
- Slidev process lifecycle management
- Presenter and participant workflows
- Ribbon-style app shell and settings
- Thumbnail, agenda, and timer MVP panels

## Architecture

- `src/SlideDevPresenter.App`: Avalonia shell and UI
- `src/SlideDevPresenter.Core`: Domain models and contracts
- `src/SlideDevPresenter.Infrastructure`: Process hosting and integrations
- `tests/SlideDevPresenter.Tests`: Unit and service-level tests

## Principles

- Reuse Slidev and Slidev presenter view
- Keep UI and business logic separated
- Use JSON-based configuration
- Keep services testable
