# SlideDevPresenter

SlideDevPresenter is a local desktop host for Slidev presentations. It provides a PowerPoint-like experience for developers by combining a presentation library, Slidev process hosting, embedded Presenter View, participant display management, settings, thumbnails, agenda, and timer support.

## Presentation Features

- Start a deck from the beginning, from the selected slide, or directly in Presenter View. Slidev remains a dev server so its presenter navigation stays synchronized with the participant view.
- Successful starts open the Presentation ribbon. While running, ribbon start buttons are unavailable; use Previous and Next to navigate Slidev steps, including incremental clicks. External-browser-only sessions use the browser's presentation controls instead.
- Show the participant window on another display, choose its target display, and swap the presenter and participant surfaces. App-side blank-screen controls are mirrored between the two views.
- Use the slide thumbnails, agenda, and presentation timer panels alongside the embedded presentation.
- Open links from a deck in embedded browser tabs or in the system browser, according to navigation settings. If embedded WebView access is denied, the app can fall back to the external browser for the session.
- Export a selected local Slidev project as a PDF with Slidev's export command. Hosted presentations cannot be exported by the app.

## Install and Update

Download an installer from [GitHub Releases](https://github.com/norschel/SlidevPresenter/releases/latest):

- **Windows x64**: run the `win-x64` Setup executable. Installation is per-user; the installer also checks for the Microsoft Edge WebView2 runtime used by the embedded browser.
- **macOS Intel**: run the `osx-x64` `.pkg` installer.
- **macOS Apple Silicon**: run the `osx-arm64` `.pkg` installer.

Stable releases also include package-manager packages:

- **Homebrew**: install from the [Homebrew tap](https://github.com/norschel/homebrew-slidedevpresenter) with `brew install --cask norschel/slidedevpresenter/slide-dev-presenter`. The cask selects the native macOS package and verifies its SHA-256 checksum. It is synchronized from stable releases automatically.
- **Chocolatey**: download the `slidedevpresenter.<version>.nupkg` asset from the latest GitHub Release, then run `choco install slidedevpresenter --source <folder-containing-the-package> --yes`. The package verifies the Windows installer checksum and supports Chocolatey uninstall. It is not currently published to the Chocolatey community feed.

The .NET runtime is included. Node.js and the presentation's package manager are still required for local presentations; Git is required only for repository sources. Settings and checkouts are stored outside the installation and are retained during updates.

Open **About > Check for updates**, then **Download update**, then **Restart and install**. Restarting stops the current presentation and saves settings first. Updates come from published stable releases by default, matching the installed platform/architecture. Enable **Include pre-release (unstable) versions** in About to also consider GitHub pre-releases, which may contain bugs. This choice applies only while that About window is open; changing it requires checking again, even if an update was already downloaded. Turning it off does not downgrade an installed pre-release; it offers a newer stable release when available. Nothing is downloaded or restarted automatically. Development builds do not enable in-app updates. If an update fails, retry or run the latest installer for the same architecture.

### Publishing a Release

Push a version tag such as `v1.2.3` or `v1.3.0-beta.1`. The **Package desktop release** workflow builds and tests the solution, then packages self-contained Windows x64 and macOS x64/arm64 apps with Velopack. Stable releases include a checksummed Homebrew cask and a Chocolatey `.nupkg`; after a stable release is published, a separate workflow pushes its cask to the [Homebrew tap](https://github.com/norschel/homebrew-slidedevpresenter). Configure the `HOMEBREW_TAP_TOKEN` Actions secret in this repository with a fine-grained personal access token that has **Contents: read and write** access to `norschel/homebrew-slidedevpresenter`. The Chocolatey package is not currently published to the community feed. The tag supplies both the application and package version. SemVer pre-release suffixes mark the draft as a GitHub pre-release; keep that flag when publishing so stable-only users do not receive unstable versions. Pre-releases use the same architecture channels and require no changes to signing configuration. To install an unstable version manually, browse [all releases](https://github.com/norschel/SlidevPresenter/releases) rather than the latest-stable download link.

The workflow creates a **draft** GitHub Release after all three packages succeed. Review the notes and test installation, launch, embedded browser, uninstall, and an update from the previous version on each platform before publishing the draft. Successful packaging triggers Homebrew publication only if the release is already public and stable. Publishing a prepared draft or promoting a prerelease to stable also synchronizes the tap without rebuilding. If a release is published before its cask is uploaded, the release event leaves the tap unchanged; packaging completion triggers publication once the assets are ready, without polling. Prerelease builds also include the package-manager assets so later promotion needs no rebuild, but drafts and prereleases never update the tap. For older prereleases missing these assets, rerun packaging before promotion. Reruns can replace draft assets; for a published release, they add only missing assets and never replace existing ones. Keep all generated assets attached, including update feeds (`releases.*.json`) and full `.nupkg` packages: the in-app updater needs them. Channels are `win-x64`, `osx-x64`, and `osx-arm64`; do not rename channels or the package ID between releases. Full packages are used, so no previous release is needed when packaging.

**Signing:** without signing secrets the workflow produces unsigned installers and does not bypass OS security checks. Windows may show SmartScreen warnings; macOS may block unsigned/unnotarized apps. For trusted public distribution, configure these GitHub Actions repository secrets before pushing the tag:

- Windows: `WINDOWS_CERTIFICATE_PFX` (base64-encoded Authenticode PFX) and `WINDOWS_CERTIFICATE_PASSWORD`. The workflow imports it into the ephemeral runner's user certificate store; Velopack signs and timestamps the application and installer.
- macOS: `MACOS_CERTIFICATE_P12` (base64-encoded export containing both Developer ID Application and Developer ID Installer certificates/private keys), `MACOS_CERTIFICATE_PASSWORD`, `MACOS_SIGN_APP_IDENTITY`, and `MACOS_SIGN_INSTALL_IDENTITY`. Supply the identity names without the parenthesized team suffix, as required by Velopack. Set `MACOS_APPLE_ID`, `MACOS_APPLE_PASSWORD` (app-specific password), and `MACOS_TEAM_ID` for notarization. The workflow uses a temporary keychain and removes it after packaging.

Never commit certificates or credentials. Providing a certificate enables signing; invalid or incomplete signing configuration fails the job rather than silently publishing unsigned output. See [Velopack signing](https://docs.velopack.io/packaging/signing) and [macOS packaging](https://docs.velopack.io/packaging/operating-systems/macos). Signed/notarized installers still need native-platform verification; a successful cross-publish alone does not verify installation or updating.

## Presentation Sources

Open **Library > Sources...** (or **Settings > Sources**) to add, edit, disable or remove sources, then select **Save**. Supported types:

- **HostedUrl**: an HTTP or HTTPS address for an already hosted presentation.
- **Local**: a local folder containing a Slidev presentation or multiple projects. **+ Local** opens the native folder selection dialog; the folder button beside **Location** changes an existing source's folder. Projects in the selected folder and its subfolders are discovered automatically.
- **Git**: an HTTP, HTTPS, SSH or `git://` repository URL, including `git@host:owner/repository.git`.
- **GitHub**: a GitHub repository URL or `owner/repository`. Use HostedUrl for GitHub Pages presentations.

Repository sources use a stable per-repository folder under the user's local application-data directory. Set **Checkout folder** to reuse a repository already cloned elsewhere, or choose another destination. Existing folders are never overwritten. Removing a source does not delete local files.

Set **Branch** to select a branch, or leave it blank to keep the current/default branch. **Load branches** suggests branches already known to the checkout; you can also type a branch name. New clones use the selected branch. For existing checkouts, a branch change asks before opening `git switch` in a terminal. Uncommitted changes block switching until you commit or stash them manually. Refresh after switching; repository refresh is blocked while a presentation is building or running. Sources pointing at the same checkout share its current branch.

Git must be installed and available to the app. Without it, Git source controls are disabled and installation guidance is displayed; local and hosted sources remain usable. Install Git from [git-scm.com](https://git-scm.com/downloads); on macOS, `xcode-select --install` or `brew install git` are alternatives. Restart the app after installation.

New repositories are cloned with the Git CLI in a separate terminal window. Authenticate there using your usual Git credentials or SSH setup; the presenter has no credential fields and rejects credentials embedded in repository URLs. After cloning completes, select **Refresh Library**. On macOS, allow Terminal automation under **System Settings > Privacy & Security > Automation** if prompted. Windows uses PowerShell; Linux requires `x-terminal-emulator`.

For existing checkouts, refresh performs a noninteractive fetch with a timeout. When the tracked branch has incoming commits, the app asks whether to open a terminal and run `git pull --ff-only`. Declining keeps the local version. Refresh again after updating. Fetch failures keep the existing local presentation available and show recovery guidance. Resolve authentication, network, upstream-branch or merge issues manually in the checkout; the presenter does not reset, stash, force-pull or discard changes.

Shared library import/export preserves repository source types and excludes local checkout paths. Existing shared lists continue to treat `github` entries as hosted URLs; new GitHub repositories use `github-repository` entries.

## Editing Presentations

In **Settings > Edit**, enable **editing in VS Code** (off by default) and choose **Stable** (regular VS Code) or **Insiders**. Only installed releases are offered; if neither is found, installation guidance is shown. Reopen Settings after installing a release.

Select a local or checked-out Git/GitHub presentation, then use **Edit > Open in VS Code** to open its project folder. The Edit tab is hidden for remote hosted URLs and when editing is disabled or the selected editor is unavailable. Changes are saved by VS Code; start the presentation again to compile the updated deck.

## Compiling Presentations

Starting a local or repository presentation first installs its dependencies and runs the local Slidev CLI's `build` command. Only after compilation succeeds does the presenter start the Slidev server. Hosted URLs do not need compiling.

Install Node.js and the project's package manager on PATH. The app honors `packageManager` in package.json, otherwise selecting npm, pnpm, Yarn or Bun from the lockfile. Dependency installation is reused until the manifests or lockfiles change, including after branch switches. Compilation runs on each start and writes Slidev's normal `dist` output. Projects need a package.json with `@slidev/cli` as a dependency or devDependency.

Compilation and dependency failures stop launch and show recovery details. Fix package-registry authentication, dependency issues or Slidev errors manually in the project folder, then retry. **Stop** cancels an in-progress install or build. Project dependency installation and compilation can execute code from the repository, so only use trusted projects.

## Development

The solution targets .NET 10. Install the .NET 10 SDK, then run these commands from the repository root:

```sh
dotnet build ./SlideDevPresenter.sln
dotnet test ./SlideDevPresenter.sln
dotnet run --project ./src/SlideDevPresenter.App/SlideDevPresenter.App.csproj
```

The build-and-test workflow also runs Release builds and tests on Ubuntu, Windows, and macOS for pushes and pull requests. Unit tests use local fakes and do not require a Git checkout, network access, or the real user settings directory.
