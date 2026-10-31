# Copilot Instructions for SlideDevPresenter

SlideDevPresenter is a cross-platform Avalonia desktop shell around [Slidev](https://sli.dev): it scans/clones slide-deck sources, builds and hosts the Slidev dev server, and drives presenter + participant windows across displays. See `README.md` for features and `docs/agent-spec.md` for scope and principles.

## Build & Test

- Build: `dotnet build ./SlideDevPresenter.sln`
- Test: `dotnet test ./SlideDevPresenter.sln` (xUnit; CI runs Release config on `ubuntu-latest`, see `.github/workflows/verify-build.yml`)
- Run app: `dotnet run --project ./src/SlideDevPresenter.App/SlideDevPresenter.App.csproj`
- Target framework is `net10.0` for all projects. Avalonia 12.x, CommunityToolkit.Mvvm 8.x, Microsoft.Extensions.Logging 10.x.
- Always build and run the tests after making code changes; fix new warnings you introduce.

## Project Layout

| Project | Purpose | Rules |
|---|---|---|
| `src/SlideDevPresenter.Core` | Domain models (`Models/`) and service contracts (`Services/I*.cs`) | No UI or third-party dependencies. |
| `src/SlideDevPresenter.Infrastructure` | Service implementations: process hosting, Git, Slidev build, settings persistence | Depends only on Core + `Microsoft.Extensions.Logging.Abstractions`. No Avalonia. |
| `src/SlideDevPresenter.App` | Avalonia UI: `Views/`, `ViewModels/`, Avalonia-specific `Services/` | Only place Avalonia types may appear. |
| `tests/SlideDevPresenter.Tests` | xUnit tests mirroring `Services/` and `ViewModels/` | One `XxxTests.cs` per class under test. |

Namespaces mirror folder paths (`SlideDevPresenter.Core.Models`, `SlideDevPresenter.App.ViewModels`, ...).

## Coding Conventions

- `Nullable` and `ImplicitUsings` are enabled everywhere; write null-safe code and do not add redundant `using` directives.
- Prefer `sealed` classes. Models are `sealed class` with `required`/`init` properties where values are mandatory or immutable.
- Interfaces use the `I` prefix. New service contracts go in Core, implementations in Infrastructure (or App when they need Avalonia).
- Async methods return `Task`/`Task<T>` and end in `Async`. Accept a `CancellationToken` where the operation can be long-running.
- Logging via `ILogger<T>` injected through constructors; use `NullLogger<T>.Instance` in tests.
- Keep UI and business logic separated: no file I/O, process launching or Git logic in Views or ViewModels—delegate to services.
- Configuration is JSON-based (`System.Text.Json`); keep settings round-trippable and backwards compatible with existing files.

## MVVM / Avalonia

- ViewModels are `sealed partial class ... : ObservableObject` using CommunityToolkit.Mvvm source generators (`[ObservableProperty]`, `[RelayCommand]`). Do not hand-write `INotifyPropertyChanged`.
- Compiled bindings are on by default (`AvaloniaUseCompiledBindingsByDefault`); set `x:DataType` on views and keep bindings statically resolvable.
- Dependency injection is manual in `src/SlideDevPresenter.App/App.axaml.cs` (`OnFrameworkInitializationCompleted`). When adding a service, construct it there and pass it via constructor; respect construction order and keep `MainViewModel` constructor parameters optional where tests rely on defaults.
- File/folder pickers use `TopLevel.StorageProvider` in Window code-behind; pass resulting `Stream`s or paths into the ViewModel rather than referencing Avalonia storage APIs from ViewModels.
- Avalonia 12 clipboard access is via the `Avalonia.Input.Platform` extension methods (e.g. `Clipboard.SetTextAsync`).
- Native WebView keyboard quirk: when a `NativeWebView` has focus, Avalonia `KeyDown` never fires. Presenter/participant key handling goes through web messages (`sdp:*`) and `PresentationWindowService.DispatchPresentationKey`; `MainWindow.TryForwardKeyToParticipant` covers the macOS key-window case. Preserve this flow when touching keyboard or window-focus code.

## Testing

- Framework: xUnit (`[Fact]`, `[Theory]` + `[InlineData]`). Test classes are `public sealed class XxxTests`; methods are named `Method_ExpectedBehavior` or `Scenario_ExpectedBehavior`.
- Prefer real implementations plus hand-written fakes over mocking frameworks. Shared fakes (`FakeSettingsService`, `FakePresentationWindowService`, `FakeDisplaySelector`, ...) live at the top of `tests/SlideDevPresenter.Tests/ViewModels/MainViewModelTests.cs`; reuse or extend them before adding new ones.
- Tests must not touch the real user settings location, launch processes, or require network/Git access; use temp directories and injected fakes (`IGitCliClient`, etc.).
- Add or update tests for any behavior change in Core or Infrastructure services and for ViewModel command logic.

## Domain Notes

- Source types (`PresentationSourceType`): `LocalRoot`, `LocalProject`, `HostedUrl`, `Git`, `GitHub`. In the library JSON they serialize as `git` / `github-repository`; a legacy `github` JSON type is a `HostedUrl`. Checkout paths are derived, never persisted.
- `GitRepositoryService` prepares/reuses clones and switches branches through `IGitCliClient`; local sources inside a Git work tree get branch selection without fetch/pull. Refresh is blocked while a presentation is running.
- `SlideDeckBuilder` installs dependencies based on the lockfile/`packageManager`, hashes manifests to skip redundant installs, then runs the local `@slidev/cli` build before `SlidevProcessHost` starts the dev server. `SourceScanner` must keep excluding `node_modules`, `.git`, and `dist`.
- Slidev runs as a dev server so navigation syncs between presenter and participant; the injected blank overlay (`b`/`w`/`.`) is app-side and must be mirrored explicitly.

## Platform-Specific Behavior

Platform branches use `OperatingSystem.IsWindows()` / `OperatingSystem.IsMacOS()` (Infrastructure, App) or an injectable `IPlatformInfo` (`ShortcutService`) so tests can cover each OS. Keep Linux as the fallback branch. When adding a platform workaround, leave a one-line comment stating the OS quirk it addresses.

### Windows

- Process launching: `npm`/`pnpm`/`yarn`/`bun` are `.cmd` shims, so `SlideDeckBuilder.CreateStartInfo` routes everything except `node` through `powershell.exe -NoProfile -NonInteractive -EncodedCommand` (script built by `GitCliClient.BuildShellCommand`, which quotes with PowerShell single-quote rules on Windows and POSIX rules elsewhere). `SlidevPdfExporter` uses `cmd.exe /d /c npx.cmd`. Follow the same pattern for any new Node tooling invocation.
- `GitCliClient.OpenTerminalAsync` opens `powershell.exe -NoExit -EncodedCommand` for interactive clone/pull/switch so credential prompts can be answered.
- Path comparisons for checkout/work-tree detection are `OrdinalIgnoreCase` on Windows, `Ordinal` elsewhere (`GitRepositoryService`).
- `CodeEditorService` resolves `Code.exe` / `Code - Insiders.exe` via `PATH` (checking the parent of `bin` folders), `%LOCALAPPDATA%\Programs`, and `ProgramFiles*`; executability is judged by `.exe` extension rather than Unix mode bits.
- WebView2: do not navigate a WebView to `about:blank` while it is being torn down after ESC; `MainWindow.SyncEmbeddedWebViewSource` keeps the last real page instead. Both WebViews stay detached from the visual tree until needed so WebView2 is not initialized at startup.
- Default start shortcuts are `F5`, `Shift+F5`, `Alt+F5` (also Linux).

### macOS

- Native WebViews own their OS airspace: toggling `IsVisible` on an Avalonia parent does not hide them. `MainWindow` detaches/re-attaches the WebView controls from their host `Panel`, deferred via `Dispatcher.UIThread.Post(..., Background)` so the detach happens after the current input cycle. Keep this when changing surface switching.
- Key routing: after swapping surfaces the main window remains the key window and `Activate()` on the participant is unreliable, so `MainWindow.TryForwardKeyToParticipant` forwards B/W/N/P/./arrows/PageUp/Down/Space/Enter to the participant window unless focus is in a `TextBox` or `NativeWebView`. OS-level ESC leaving fullscreen never reaches Avalonia; `ParticipantWindow.OnPropertyChanged` treats a `FullScreen -> other` state change as an exit request.
- Fullscreen is asynchronous: `ParticipantWindow.MoveTo` leaves fullscreen, re-positions, re-enters fullscreen on the dispatcher, and retries `FocusPresentation` after ~900 ms because activation is ignored during the animation. The keyboard bridge script must be re-injected after such moves.
- `GitCliClient.OpenTerminalAsync` drives Terminal.app via `osascript`; failures surface a hint to allow automation under System Settings > Privacy & Security > Automation.
- `CodeEditorService` looks for `code`/`code-insiders` inside `/Applications` and `~/Applications` bundles, then `/usr/local/bin` and `/opt/homebrew/bin`; GUI apps do not inherit the shell `PATH`, so keep these explicit fallbacks.
- Default start shortcuts are `⌘+Shift+Return`, `⌘+Return`, `Option+Return`; use `KeyModifiers.Meta` for the Command key.
- Temporary .NET harnesses must use canonical `/private/tmp` paths; `/tmp` symlink paths can break transitive MSBuild project references.

## Don'ts

- Don't introduce a DI container, mocking library, or additional MVVM framework.
- Don't reference Avalonia from Core or Infrastructure.
- Don't commit `bin/`, `obj/`, or generated Slidev output.
- Don't add documentation files for changes unless asked; update `README.md` or `docs/agent-spec.md` when behavior or scope changes.
