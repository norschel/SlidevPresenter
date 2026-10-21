# SlideDevPresenter

SlideDevPresenter is a local desktop host for Slidev presentations. It provides a PowerPoint-like experience for developers by combining a presentation library, Slidev process hosting, embedded Presenter View, participant display management, settings, thumbnails, agenda, and timer support.

## Install and Update

Download an installer from [GitHub Releases](https://github.com/norschel/SlidevPresenter/releases/latest):

- **Windows x64**: run the `win-x64` Setup executable. Installation is per-user; the installer also checks for the Microsoft Edge WebView2 runtime used by the embedded browser.
- **macOS Intel**: choose the `osx-x64` installer.
- **macOS Apple Silicon**: choose the `osx-arm64` installer.

The .NET runtime is included. Node.js and the presentation's package manager are still required for local presentations; Git is required only for repository sources. Settings and checkouts are stored outside the installation and are retained during updates.

Open **About > Check for updates**, then **Download update**, then **Restart and install**. Restarting stops the current presentation and saves settings first. Updates come only from published stable releases in this repository, matching the installed platform/architecture. Nothing is downloaded or restarted automatically. Development builds do not enable in-app updates. If an update fails, retry or run the latest installer for the same architecture.

### Publishing a Release

Push a stable version tag such as `v1.2.3`. The **Package desktop release** workflow builds and tests the solution, then packages self-contained Windows x64 and macOS x64/arm64 apps with Velopack. The tag supplies both the application and package version; tags with prerelease suffixes are rejected.

The workflow creates a **draft** GitHub Release after all three packages succeed. Review the notes and test installation, launch, embedded browser, uninstall, and an update from the previous version on each platform before publishing the draft. Reruns can replace draft assets, but refuse to overwrite a published release. Keep all generated assets attached, including update feeds (`releases.*.json`) and full `.nupkg` packages: the in-app updater needs them. Channels are `win-x64`, `osx-x64`, and `osx-arm64`; do not rename channels or the package ID between releases. Full packages are used, so no previous release is needed when packaging.

**Signing:** the default workflow produces unsigned installers and does not bypass OS security checks. Windows may show SmartScreen warnings; macOS may block unsigned/unnotarized apps. For trusted public distribution, configure Windows Authenticode signing and Apple Developer ID signing/notarization in the packaging step before publishing. Keep certificates, passwords, and Apple credentials in GitHub Actions secrets, never in the repository. See [Velopack Windows signing](https://docs.velopack.io/packaging/windows/code-signing) and [macOS packaging](https://docs.velopack.io/packaging/osx). Signed/notarized installers still need native-platform verification; a successful cross-publish alone does not verify installation or updating.

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
