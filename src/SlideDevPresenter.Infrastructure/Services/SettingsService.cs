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
            await WriteSettingsAsync(Settings, cancellationToken);
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
        using var document = await JsonDocument.ParseAsync(source, cancellationToken: cancellationToken);
        var requiredSections = new[]
        {
            nameof(AppSettings.Sources),
            nameof(AppSettings.Defaults),
            nameof(AppSettings.Appearance),
            nameof(AppSettings.WebView),
            nameof(AppSettings.DisplayManagement),
            nameof(AppSettings.Shortcuts),
            nameof(AppSettings.Navigation)
        };
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || requiredSections.Any(section => !document.RootElement.EnumerateObject()
                .Any(property => string.Equals(property.Name, section, StringComparison.OrdinalIgnoreCase))))
        {
            throw new InvalidDataException("The configuration file is missing required settings.");
        }

        var importedSettings = document.RootElement.Deserialize<AppSettings>(JsonOptions)
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

        await WriteSettingsAsync(importedSettings, cancellationToken);
        Settings = importedSettings;
    }

    private async Task WriteSettingsAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        var dir = Path.GetDirectoryName(_settingsFilePath);
        if (string.IsNullOrEmpty(dir))
            throw new IOException($"Cannot determine directory for settings path {_settingsFilePath}.");

        Directory.CreateDirectory(dir);

        await using var stream = File.Open(_settingsFilePath, FileMode.Create, FileAccess.Write);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
        _logger.LogInformation("Settings saved to {Path}.", _settingsFilePath);
    }

    private static string GetDefaultSettingsPath()
    {
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(folder, "SlideDevPresenter", "settings.json");
    }
}
