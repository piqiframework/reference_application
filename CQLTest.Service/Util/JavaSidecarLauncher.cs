using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace CQLTest.Service;

public class JavaSidecarOptions
{
    public const string Section = "JavaSidecar";
    /// <summary>Path to the sidecar JAR, relative to the app base directory or absolute.</summary>
    public string JarPath { get; set; } = @"..\lib\cql-sidecar-1.0.0.jar";
    /// <summary>How long to wait for the sidecar to become healthy on startup.</summary>
    public int StartupTimeoutSeconds { get; set; } = 60;
    /// <summary>Base URL used for health polling — should match the sidecar's server.port.</summary>
    public string BaseUrl { get; set; } = "http://localhost:8080";

    // ── Terminology (FHIR) settings passed to the sidecar at launch ──────────
    /// <summary>FHIR R4 base URL for value set expansion. Leave blank to skip value set resolution.</summary>
    public string? TerminologyFhirBaseUrl { get; set; }
    /// <summary>Skip SSL certificate verification for the terminology server.</summary>
    public bool TerminologySkipSslVerify { get; set; } = false;
    /// <summary>Auth type for the terminology server: None, Basic, Bearer.</summary>
    public string TerminologyAuthType { get; set; } = "None";
    public string? TerminologyUsername { get; set; }
    public string? TerminologyPassword { get; set; }
    public string? TerminologyToken { get; set; }
}

public sealed class JavaSidecarLauncher(IOptions<JavaSidecarOptions> opts, ILogger<JavaSidecarLauncher> logger, IHostEnvironment hostEnvironment) : IDisposable
{
    private readonly JavaSidecarOptions _opts = opts.Value;
    private readonly bool _isDevelopment = hostEnvironment.IsDevelopment();
    private Process? _process;

    /// <summary>
    /// Starts the Java sidecar and waits until /actuator/health returns 200
    /// or the startup timeout elapses.
    /// </summary>
    public async Task StartAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        // If a sidecar is already running (e.g. from a previous run or another process), reuse it.
        using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) })
        {
            try
            {
                var resp = await http.GetAsync($"{_opts.BaseUrl.TrimEnd('/')}/actuator/health", ct);
                if (resp.IsSuccessStatusCode)
                {
                    logger.LogInformation("CQL sidecar already running at {Url} — skipping launch.", _opts.BaseUrl);
                    progress?.Report("CQL engine ready ✓");
                    return;
                }
            }
            catch { /* not running yet, proceed to launch */ }
        }

        var jarPath = Path.IsPathRooted(_opts.JarPath)
            ? _opts.JarPath
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, _opts.JarPath));

        if (!File.Exists(jarPath))
            throw new FileNotFoundException(
                $"CQL sidecar JAR not found at '{jarPath}'. Build the JavaSidecar project first (mvn package).", jarPath);

        var java = FindJava();

        // Build Spring Boot command-line property overrides
        var args = new System.Text.StringBuilder($"-jar \"{jarPath}\"");
        if (!string.IsNullOrWhiteSpace(_opts.TerminologyFhirBaseUrl))
            args.Append($" --cql.terminology.fhir-base-url={_opts.TerminologyFhirBaseUrl}");
        args.Append($" --cql.terminology.skip-ssl-verify={_opts.TerminologySkipSslVerify.ToString().ToLower()}");
        if (_opts.TerminologyAuthType != "None")
            args.Append($" --cql.terminology.auth-type={_opts.TerminologyAuthType}");
        if (!string.IsNullOrWhiteSpace(_opts.TerminologyUsername))
            args.Append($" --cql.terminology.username={_opts.TerminologyUsername}");
        if (!string.IsNullOrWhiteSpace(_opts.TerminologyPassword))
            args.Append($" --cql.terminology.password={_opts.TerminologyPassword}");
        if (!string.IsNullOrWhiteSpace(_opts.TerminologyToken))
            args.Append($" --cql.terminology.token={_opts.TerminologyToken}");
        if (_isDevelopment)
            args.Append(" --logging.level.com.cqlsidecar=DEBUG");

        logger.LogInformation("Starting CQL sidecar: {Java} -jar {Jar}", java, jarPath);
        progress?.Report("Starting CQL engine sidecar…");

        var psi = new ProcessStartInfo(java, args.ToString())
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        _process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start Java process.");

        // Drain stdout/stderr to avoid blocking
        _process.OutputDataReceived += (_, e) => { if (e.Data != null) logger.LogDebug("[sidecar] {Line}", e.Data); };
        _process.ErrorDataReceived  += (_, e) => { if (e.Data != null) logger.LogDebug("[sidecar] {Line}", e.Data); };
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        // Poll /actuator/health until healthy or timeout
        await WaitForHealthyAsync(progress, ct);

        progress?.Report("CQL engine ready ✓");
        logger.LogInformation("CQL sidecar is healthy.");
    }

    private async Task WaitForHealthyAsync(IProgress<string>? progress, CancellationToken ct)
    {
        // Use a generous per-attempt timeout; ct is checked explicitly so VS doesn't
        // surface first-chance TaskCanceledExceptions from the health-check HTTP call.
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var healthUrl = $"{_opts.BaseUrl.TrimEnd('/')}/actuator/health";
        var deadline = DateTime.UtcNow.AddSeconds(_opts.StartupTimeoutSeconds);
        var attempt = 0;

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            if (_process is { HasExited: true })
                throw new InvalidOperationException(
                    $"CQL sidecar process exited unexpectedly (code {_process.ExitCode}).");

            attempt++;
            progress?.Report($"Waiting for CQL engine… (attempt {attempt})");

            try
            {
                // Pass CancellationToken.None — timeout is controlled by HttpClient.Timeout above.
                // User cancellation is handled by ct.ThrowIfCancellationRequested() each iteration.
                var response = await http.GetAsync(healthUrl, CancellationToken.None);
                if (response.IsSuccessStatusCode) return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { /* not up yet */ }

            await Task.Delay(1000, ct);
        }

        throw new TimeoutException(
            $"CQL sidecar did not become healthy within {_opts.StartupTimeoutSeconds}s.");
    }

    private static string FindJava()
    {
        // 1. JAVA_HOME env var
        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrEmpty(javaHome))
        {
            var candidate = Path.Combine(javaHome, "bin", "java.exe");
            if (File.Exists(candidate)) return candidate;
        }

        // 2. java.exe on PATH
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
        {
            var candidate = Path.Combine(dir.Trim(), "java.exe");
            if (File.Exists(candidate)) return candidate;
        }

        throw new FileNotFoundException(
            "java.exe not found. Set JAVA_HOME or add Java to PATH.");
    }

    public void Dispose()
    {
        bool isRunning = false;
        try { isRunning = _process is { HasExited: false }; } catch { /* process object has no OS association */ }

        if (isRunning && _process != null)
        {
            try
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(3000);
            }
            catch { /* best-effort shutdown — logger may already be disposed */ }
        }
        _process?.Dispose();
    }
}

/// <summary>
/// Starts/stops the Java CQL sidecar as part of the ASP.NET Core host lifecycle.
/// </summary>
public sealed class JavaSidecarHostedService(JavaSidecarLauncher launcher) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) =>
        launcher.StartAsync(ct: cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken)
    {
        launcher.Dispose();
        return Task.CompletedTask;
    }
}
