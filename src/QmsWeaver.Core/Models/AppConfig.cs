namespace QmsWeaver.Core.Models;

public sealed class AppConfig
{
    public List<FolderBinding> FolderBindings { get; set; } = new();
    public AiSettings Ai { get; set; } = new();
    public string Theme { get; set; } = "system"; // system | light | dark

    /// <summary>최초 실행 온보딩(ISO 13485 기본 구조 안내 + 폴더 연결 제안)을 이미 보여줬는가.</summary>
    public bool OnboardingShown { get; set; }

    /// <summary>UI 폰트 크기 (pt). 설정에서 조절 가능.</summary>
    public double UiFontSize { get; set; } = 13;

    /// <summary>테이블 행 높이 (px) — 행간 간격 조절.</summary>
    public double TableRowHeight { get; set; } = 34;
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
