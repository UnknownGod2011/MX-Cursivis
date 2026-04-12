using Cursivis.Companion.Models;
using System.IO;
using System.Text.Json;

namespace Cursivis.Companion.Services;

public sealed class RuntimeLaunchProfileService
{
    private const string ProfileFileName = "runtime-profile.json";
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string _profileDir;
    private readonly string _profilePath;

    public RuntimeLaunchProfileService()
    {
        _profileDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cursivis");
        _profilePath = Path.Combine(_profileDir, ProfileFileName);
    }

    public async Task<RuntimeLaunchProfile?> TryLoadAsync()
    {
        if (!File.Exists(_profilePath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_profilePath);
            return JsonSerializer.Deserialize<RuntimeLaunchProfile>(json, _jsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveAsync(RuntimeLaunchProfile profile)
    {
        Directory.CreateDirectory(_profileDir);
        var json = JsonSerializer.Serialize(profile, _jsonOptions);
        await File.WriteAllTextAsync(_profilePath, json);
    }

    public async Task<bool> UpdateApiKeysAsync(string apiKey)
    {
        var normalized = string.Join(
            ",",
            (apiKey ?? string.Empty)
                .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal));

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        var profile = await TryLoadAsync();
        if (profile is null)
        {
            return false;
        }

        profile.ApiKey = normalized.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? normalized;
        profile.ApiKeys = normalized;
        await SaveAsync(profile);
        return true;
    }
}
