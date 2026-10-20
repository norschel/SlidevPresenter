using System.Diagnostics;
using SlideDevPresenter.Core.Models;
using SlideDevPresenter.Core.Services;

namespace SlideDevPresenter.Infrastructure.Services;

public sealed class SlidevPdfExporter : ISlideDeckPdfExporter
{
    public async Task ExportPdfAsync(PresentationProject project, string outputPath, CancellationToken cancellationToken = default)
    {
        if (project.SourceType != PresentationSourceType.LocalProject)
            throw new ArgumentException("Only local Slidev projects can be exported.", nameof(project));

        using var process = new Process { StartInfo = CreateStartInfo(project, outputPath) };
        process.Start();

        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var error = await standardError;
        await standardOutput;

        if (process.ExitCode != 0)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error)
                ? $"Slidev PDF export failed with exit code {process.ExitCode}."
                : error.Trim());
    }

    internal static ProcessStartInfo CreateStartInfo(PresentationProject project, string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("An output path is required.", nameof(outputPath));

        var slidesPath = Path.GetFullPath(project.SlidesFilePath ?? Path.Combine(project.Location, "slides.md"));
        var startInfo = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "npx",
            WorkingDirectory = project.Location,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        if (OperatingSystem.IsWindows())
        {
            startInfo.ArgumentList.Add("/d");
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add("npx.cmd");
        }
        startInfo.ArgumentList.Add("slidev");
        startInfo.ArgumentList.Add("export");
        startInfo.ArgumentList.Add(slidesPath);
        startInfo.ArgumentList.Add("--format");
        startInfo.ArgumentList.Add("pdf");
        startInfo.ArgumentList.Add("--output");
        startInfo.ArgumentList.Add(Path.GetFullPath(outputPath));
        return startInfo;
    }
}
