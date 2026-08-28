using System.Text.Json;
using QmsAnalyzer.Core.Models;

namespace QmsAnalyzer.Core.Services;

/// <summary>%AppData%/QmsAnalyzer/config.json 로드·저장. API 키는 이 로컬 파일에만 존재한다.</summary>
public sealed class ConfigService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public string ConfigDir { get; }
    public string ConfigPath { get; }
    public AppConfig Config { get; private set; } = new();

    public ConfigService(string? baseDir = null)
    {
        ConfigDir = baseDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QmsAnalyzer");
        ConfigPath = Path.Combine(ConfigDir, "config.json");
        Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
                Config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigPath)) ?? new();
        }
        catch
        {
            Config = new(); // 손상된 설정은 기본값으로 복구
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(ConfigDir);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(Config, JsonOpts));
    }
}
