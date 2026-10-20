using System.Diagnostics;
using System.Text;

namespace SlideDevPresenter.Infrastructure.Services;

public interface IGitCliClient
{
    Task<(int ExitCode, string Output)> RunAsync(string? directory, params string[] arguments);
    Task OpenTerminalAsync(params string[] arguments);
}

public sealed class GitCliClient(string gitExecutable = "git") : IGitCliClient
{
    public async Task<(int ExitCode, string Output)> RunAsync(string? directory, params string[] arguments)
    {
        try
        {
            var info = new ProcessStartInfo(gitExecutable)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            if (directory is not null)
                info.WorkingDirectory = directory;
            info.Environment["GIT_TERMINAL_PROMPT"] = "0";
            info.Environment["GCM_INTERACTIVE"] = "Never";
            info.Environment["GIT_SSH_COMMAND"] = "ssh -o BatchMode=yes -o ConnectTimeout=10";
            info.ArgumentList.Add("-c");
            info.ArgumentList.Add("core.askPass=");
            info.ArgumentList.Add("-c");
            info.ArgumentList.Add("credential.interactive=false");
            foreach (var argument in arguments)
                info.ArgumentList.Add(argument);
            using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start Git.");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
                await Task.WhenAll(output, error);
                return (-1, "Git timed out.");
            }
            await error;
            return (process.ExitCode, await output);
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        {
            return (-1, exception.Message);
        }
    }

    public static string BuildShellCommand(params string[] arguments) => string.Join(" ", arguments.Select(argument =>
        OperatingSystem.IsWindows() ? "'" + argument.Replace("'", "''") + "'" : "'" + argument.Replace("'", "'\"'\"'") + "'"));

    public async Task OpenTerminalAsync(params string[] arguments)
    {
        var command = BuildShellCommand([gitExecutable, .. arguments]);
        ProcessStartInfo info;
        if (OperatingSystem.IsMacOS())
        {
            info = new("osascript") { UseShellExecute = false, RedirectStandardError = true };
            info.ArgumentList.Add("-e");
            info.ArgumentList.Add("tell application \"Terminal\"\nactivate\ndo script \"" + command.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r") + "\"\nend tell");
        }
        else if (OperatingSystem.IsWindows())
        {
            var script = "& " + command + "; Write-Host 'If Git failed, fix it manually here. Refresh the presenter after completion.'";
            info = new("powershell.exe") { UseShellExecute = true };
            info.ArgumentList.Add("-NoExit");
            info.ArgumentList.Add("-EncodedCommand");
            info.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(script)));
        }
        else
        {
            info = new("x-terminal-emulator") { UseShellExecute = false };
            info.ArgumentList.Add("-e");
            info.ArgumentList.Add("sh");
            info.ArgumentList.Add("-c");
            info.ArgumentList.Add(command + "; printf '\\nFix any Git errors here, then refresh the presenter. Press Enter to close.'; read answer");
        }
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not open a terminal.");
        if (OperatingSystem.IsMacOS())
        {
            var error = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
                throw new InvalidOperationException("Terminal could not be opened. Allow Terminal automation in System Settings > Privacy & Security > Automation. " + await error);
        }
    }
}