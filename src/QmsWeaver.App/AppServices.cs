using QmsWeaver.Core.Models;
using QmsWeaver.Core.Services;

namespace QmsWeaver.App;

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

    /// <param name="configDir">테스트/시뮬레이션용 설정 디렉토리 격리 (null = 사용자 AppData)</param>
    public AppServices(string? configDir = null)
    {
        Network = new SeedNetworkService();
        Config = new ConfigService(configDir);
        Audit = new AuditService(Config.ConfigDir);
        Scanner = new FolderScanService(Network);
        Rules = new RulesEngine(Network);
        Ai = new AiService(() => Config.Config.Ai);
        Audit.Log("app.start");
    }

    /// <summary>스캔 진행 상태 텍스트 (진행 중 실시간 갱신).</summary>
    public string ScanStatus { get; private set; } = "";
    public bool IsScanning { get; private set; }
    public event Action? ScanProgressChanged;

    /// <summary>
    /// 폴더 재스캔 — 진행률을 실시간 보고하고, 배치 단위로 부분 결과를 즉시 목록에 반영한다.
    /// (요구: '스캔 중'만 떠 있지 않고, 스캔되는 대로 데이터가 추가되어야 함)
    /// </summary>
    public async Task RescanAsync()
    {
        if (IsScanning) return;
        IsScanning = true;
        var bindings = Config.Config.FolderBindings.ToList();
        var partial = new List<RecordEntry>();

        // Progress<T>는 UI 스레드에서 생성되므로 콜백이 UI 컨텍스트로 전달된다.
        var progress = new Progress<FolderScanService.ScanProgress>(p =>
        {
            ScanStatus = p.CurrentDir.Length == 0
                ? $"정리 중… 파일 {p.FilesSeen:n0}개 검토 · 기록 {p.RecordsFound:n0}건"
                : $"스캔 중… 파일 {p.FilesSeen:n0}개 · 기록 {p.RecordsFound:n0}건 · {Shorten(p.CurrentDir)}";
            ScanProgressChanged?.Invoke();
        });

        void OnBatch(IReadOnlyList<RecordEntry> batch)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                partial.AddRange(batch);
                Records = partial.ToArray(); // 부분 결과 즉시 노출
                RecordsChanged?.Invoke();
            });
        }

        try
        {
            var records = await Task.Run(() => Scanner.Scan(bindings, progress, OnBatch));
            Records = records; // 후처리(개정본·형식 통합) 반영된 최종본
            ScanStatus = $"스캔 완료 — 기록 {records.Count:n0}건";
            Audit.Log("scan", detail: $"{bindings.Count}개 폴더 · 기록 {records.Count}건");
        }
        finally
        {
            IsScanning = false;
            ScanProgressChanged?.Invoke();
            RecordsChanged?.Invoke();
        }
    }

    private static string Shorten(string path) =>
        path.Length <= 46 ? path : "…" + path[^45..];

    public int RecordCount(string nodeId) => Records.Count(r => r.NodeId == nodeId);
}
