using Cursivis.Companion.Models;
using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace Cursivis.Companion.Services;

public sealed class RuntimeBootstrapper
{
    private readonly RuntimeLaunchProfileService _profileService = new();

    public async Task EnsureRuntimeReadyAsync(CancellationToken cancellationToken)
    {
        var profile = await _profileService.TryLoadAsync();
        if (profile is null)
        {
            return;
        }

        await EnsureBackendAsync(profile, cancellationToken);
        await EnsureBrowserAgentAsync(profile, cancellationToken);
        await EnsureExtensionBridgeAsync(profile, cancellationToken);
    }

    private static async Task EnsureBackendAsync(RuntimeLaunchProfile profile, CancellationToken cancellationToken)
    {
        if (await IsHealthyAsync($"{profile.BackendUrl.TrimEnd('/')}/health", cancellationToken))
        {
            return;
        }

        if (!Directory.Exists(profile.BackendDir))
        {
            return;
        }

        var commandParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(profile.ApiKey))
        {
            commandParts.Add($"$env:GOOGLE_API_KEY='{EscapeForSingleQuotedPowerShell(profile.ApiKey)}'");
        }

        if (!string.IsNullOrWhiteSpace(profile.ApiKeys))
        {
            commandParts.Add($"$env:GOOGLE_API_KEYS='{EscapeForSingleQuotedPowerShell(profile.ApiKeys)}'");
        }

        commandParts.Add("$env:GEMINI_ROUTER_MODEL='gemini-2.5-flash-lite'");
        commandParts.Add("$env:GEMINI_OPTIONS_MODEL='gemini-2.5-flash-lite'");
        commandParts.Add("$env:GEMINI_FALLBACK_MODELS='gemini-2.5-flash-lite,gemini-2.0-flash'");
        commandParts.Add($"Set-Location -LiteralPath '{EscapeForSingleQuotedPowerShell(profile.BackendDir)}'");
        commandParts.Add("npm start");

        StartHiddenPowerShell([.. commandParts]);
        await WaitForHealthyAsync($"{profile.BackendUrl.TrimEnd('/')}/health", TimeSpan.FromSeconds(30), cancellationToken);
    }

    private static async Task EnsureBrowserAgentAsync(RuntimeLaunchProfile profile, CancellationToken cancellationToken)
    {
        if (await IsHealthyAsync($"{profile.BrowserAgentUrl.TrimEnd('/')}/health", cancellationToken))
        {
            return;
        }

        if (!Directory.Exists(profile.BrowserAgentDir))
        {
            return;
        }

        StartHiddenPowerShell(
            "$env:CURSIVIS_BROWSER_CHANNEL='chrome'",
            $"Set-Location -LiteralPath '{EscapeForSingleQuotedPowerShell(profile.BrowserAgentDir)}'",
            "npm start");

        await WaitForHealthyAsync($"{profile.BrowserAgentUrl.TrimEnd('/')}/health", TimeSpan.FromSeconds(20), cancellationToken);
    }

    private static async Task EnsureExtensionBridgeAsync(RuntimeLaunchProfile profile, CancellationToken cancellationToken)
    {
        if (await IsHealthyAsync($"{profile.ExtensionBridgeUrl.TrimEnd('/')}/health", cancellationToken))
        {
            return;
        }

        var launchCmd = Path.Combine(profile.ExtensionBridgeDir, "launch.cmd");
        if (!File.Exists(launchCmd))
        {
            return;
        }

        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{launchCmd}\"",
            WorkingDirectory = profile.ExtensionBridgeDir,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        Process.Start(psi);
        await WaitForHealthyAsync($"{profile.ExtensionBridgeUrl.TrimEnd('/')}/health", TimeSpan.FromSeconds(15), cancellationToken);
    }

    private static void StartHiddenPowerShell(params string[] commandParts)
    {
        var command = string.Join("; ", commandParts);
        var psi = new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        Process.Start(psi);
    }

    private static async Task<bool> IsHealthyAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            using var response = await client.GetAsync(url, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static async Task WaitForHealthyAsync(string url, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            if (await IsHealthyAsync(url, cancellationToken))
            {
                return;
            }

            await Task.Delay(500, cancellationToken);
        }
    }

    private static string EscapeForSingleQuotedPowerShell(string value)
    {
        return value.Replace("'", "''");
    }
}
