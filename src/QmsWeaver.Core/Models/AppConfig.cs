namespace QmsWeaver.Core.Models;

public sealed class AppConfig
{
    public List<FolderBinding> FolderBindings { get; set; } = new();
    public AiSettings Ai { get; set; } = new();
    public string Theme { get; set; } = "system"; // system | light | dark
}

public sealed class AiSettings
{
    /// <summary>Anthropic Messages API 기본 엔드포인트. 사내 프록시 사용 시 교체 가능.</summary>
    public string BaseUrl { get; set; } = "https://api.anthropic.com";
    public string Model { get; set; } = "claude-opus-5";
    /// <summary>API 키는 로컬 설정 파일에만 저장된다(리포지토리·로그에 남기지 않음).</summary>
    public string ApiKey { get; set; } = "";
    public bool Enabled => !string.IsNullOrWhiteSpace(ApiKey);
}
