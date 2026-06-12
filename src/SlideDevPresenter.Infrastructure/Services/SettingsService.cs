using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using SlideDevPresenter.Core.Models;
using SlideDevPresenter.Core.Services;

namespace SlideDevPresenter.Infrastructure.Services;

public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly string _settingsFilePath;
    private readonly ILogger<SettingsService> _logger;

    public AppSettings Settings { get; private set; } = new();

    public SettingsService(ILogger<SettingsService> logger)
        : this(logger, GetDefaultSettingsPath()) { }

    internal SettingsService(ILogger<SettingsService> logger, string settingsFilePath)
    {
        _logger = logger;
        _settingsFilePath = settingsFilePath;
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsFilePath))
        {
            _logger.LogInformation("Settings file not found at {Path}. Using defaults.", _settingsFilePath);
            Settings = new AppSettings();
            return;
        }

        try
        {
            await using var stream = File.OpenRead(_settingsFilePath);
            Settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken)
                       ?? new AppSettings();
            _logger.LogInformation("Settings loaded from {Path}.", _settingsFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings from {Path}. Using defaults.", _settingsFilePath);
            Settings = new AppSettings();
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var dir = Path.GetDirectoryName(_settingsFilePath)!;
            Directory.CreateDirectory(dir);

            await using var stream = File.Open(_settingsFilePath, FileMode.Create, FileAccess.Write);
            await JsonSerializer.SerializeAsync(stream, Settings, JsonOptions, cancellationToken);
            _logger.LogInformation("Settings saved to {Path}.", _settingsFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings to {Path}.", _settingsFilePath);
        }
    }

    private static string GetDefaultSettingsPath()
    {
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(folder, "SlideDevPresenter", "settings.json");
    }
}
