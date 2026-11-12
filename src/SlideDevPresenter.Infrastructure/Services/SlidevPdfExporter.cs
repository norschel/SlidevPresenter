using System.Diagnostics;
using SlideDevPresenter.Core.Models;
using SlideDevPresenter.Core.Services;

namespace SlideDevPresenter.Infrastructure.Services;

internal interface IPdfExportProcess : IDisposable
{
    bool HasExited { get; }
    int ExitCode { get; }
    void Start();
    void Kill(bool entireProcessTree);
    Task<string> ReadStandardOutputAsync();
    Task<string> ReadStandardErrorAsync();
    Task WaitForExitAsync(CancellationToken cancellationToken);
}

internal sealed class PdfExportProcessAdapter(Process process) : IPdfExportProcess
{
    public bool HasExited => process.HasExited;
    public int ExitCode => process.ExitCode;
    public void Start() => process.Start();
    public void Kill(bool entireProcessTree) => process.Kill(entireProcessTree);
    public Task<string> ReadStandardOutputAsync() => process.StandardOutput.ReadToEndAsync();
    public Task<string> ReadStandardErrorAsync() => process.StandardError.ReadToEndAsync();
    public Task WaitForExitAsync(CancellationToken cancellationToken) => process.WaitForExitAsync(cancellationToken);
    public void Dispose() => process.Dispose();
}

public sealed class SlidevPdfExporter : ISlideDeckPdfExporter
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(10);
    private readonly Func<ProcessStartInfo, IPdfExportProcess> _processFactory;
    private readonly TimeSpan _timeout;

    public SlidevPdfExporter() : this(info => new PdfExportProcessAdapter(new Process { StartInfo = info }), DefaultTimeout) { }

    internal SlidevPdfExporter(Func<ProcessStartInfo, IPdfExportProcess> processFactory, TimeSpan? timeout = null)
    {
        _processFactory = processFactory;
        _timeout = timeout ?? DefaultTimeout;
    }

    public async Task ExportPdfAsync(PresentationProject project, string outputPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (project.SourceType != PresentationSourceType.LocalProject)
            throw new ArgumentException("Only local Slidev projects can be exported.", nameof(project));

        using var process = _processFactory(CreateStartInfo(project, outputPath));
        cancellationToken.ThrowIfCancellationRequested();
        process.Start();
        var standardOutput = process.ReadStandardOutputAsync();
        var standardError = process.ReadStandardErrorAsync();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            Exception? killFailure = null;
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception exception)
                {
                    killFailure = exception;
                }
            }

            bool hasExited;
            try
            {
                hasExited = process.HasExited;
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "Slidev PDF export was canceled, but its process state could not be verified during cleanup.",
                    killFailure is null ? exception : new AggregateException(killFailure, exception));
            }

            if (!hasExited)
                throw new InvalidOperationException(
                    "Slidev PDF export was canceled, but the process could not be terminated and may still be running.",
                    killFailure);

            await process.WaitForExitAsync(CancellationToken.None);
            await Task.WhenAll(standardOutput, standardError);

            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Slidev PDF export timed out. Check the project and retry.");
        }

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
