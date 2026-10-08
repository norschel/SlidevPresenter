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
            var dir = Path.GetDirectoryName(_settingsFilePath);
            if (string.IsNullOrEmpty(dir))
            {
                _logger.LogError("Cannot determine directory for settings path {Path}.", _settingsFilePath);
                return;
            }

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

    public async Task ExportAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        await JsonSerializer.SerializeAsync(destination, Settings, JsonOptions, cancellationToken);
    }

    public async Task ImportAsync(Stream source, CancellationToken cancellationToken = default)
    {
        var importedSettings = await JsonSerializer.DeserializeAsync<AppSettings>(source, JsonOptions, cancellationToken)
                              ?? throw new InvalidDataException("The configuration file is empty or invalid.");

        if (importedSettings.Sources is null
            || importedSettings.Defaults is null
            || importedSettings.Appearance is null
            || importedSettings.WebView is null
            || importedSettings.DisplayManagement is null
            || importedSettings.Shortcuts is null
            || importedSettings.Navigation is null
            || importedSettings.Sources.Any(source => source is null || source.Name is null || source.Location is null))
        {
            throw new InvalidDataException("The configuration file is missing required settings.");
        }

        Settings = importedSettings;
        await SaveAsync(cancellationToken);
    }

    private static string GetDefaultSettingsPath()
    {
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(folder, "SlideDevPresenter", "settings.json");
    }
}
