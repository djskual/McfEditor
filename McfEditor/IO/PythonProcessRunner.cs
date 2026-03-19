using System.Diagnostics;
using System.Text;
using System.IO;

namespace McfEditor.IO;

public sealed class PythonProcessRunner
{
    public async Task<ProcessRunResult> RunAsync(
        string pythonExecutable,
        string scriptPath,
        IEnumerable<string> arguments,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = pythonExecutable,
            WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(scriptPath) ?? Environment.CurrentDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        psi.ArgumentList.Add(scriptPath);
        foreach (var argument in arguments)
            psi.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
                stdout.AppendLine(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
                stderr.AppendLine(e.Data);
        };

        if (!process.Start())
            throw new InvalidOperationException("Failed to start python process.");

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        return new ProcessRunResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = stdout.ToString().Trim(),
            StandardError = stderr.ToString().Trim()
        };
    }
}

public sealed class ProcessRunResult
{
    public int ExitCode { get; set; }
    public string StandardOutput { get; set; } = string.Empty;
    public string StandardError { get; set; } = string.Empty;
}
