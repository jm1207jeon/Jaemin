using QmsAnalyzer.Core.Models;
using QmsAnalyzer.Core.Services;

namespace QmsAnalyzer.App;

/// <summary>앱 전역 서비스 컨테이너와 공유 상태.</summary>
public sealed class AppServices
{
    public SeedNetworkService Network { get; }
    public ConfigService Config { get; }
    public AuditService Audit { get; }
    public FolderScanService Scanner { get; }
    public RulesEngine Rules { get; }
    public AiService Ai { get; }

    /// <summary>마지막 폴더 스캔 결과 (기록 레이어).</summary>
    public IReadOnlyList<RecordEntry> Records { get; private set; } = Array.Empty<RecordEntry>();

    public event Action? RecordsChanged;

    public AppServices()
    {
        Network = new SeedNetworkService();
        Config = new ConfigService();
        Audit = new AuditService(Config.ConfigDir);
        Scanner = new FolderScanService(Network);
        Rules = new RulesEngine(Network);
        Ai = new AiService(() => Config.Config.Ai);
        Audit.Log("app.start");
    }

    public async Task RescanAsync()
    {
        var bindings = Config.Config.FolderBindings;
        var records = await Task.Run(() => Scanner.Scan(bindings));
        Records = records;
        Audit.Log("scan", detail: $"{bindings.Count}개 폴더 · 기록 {records.Count}건");
        RecordsChanged?.Invoke();
    }

    public int RecordCount(string nodeId) => Records.Count(r => r.NodeId == nodeId);
}
