using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QmsAnalyzer.Core.Models;
using QmsAnalyzer.Core.Services;

namespace QmsAnalyzer.App.ViewModels;

public sealed class LineageCard
{
    public string Level { get; init; } = "";
    public string Name { get; init; } = "";
    public string Sub { get; init; } = "";
    public bool IsTarget { get; init; }
}

public sealed class FindingRow
{
    public ReviewFinding Finding { get; init; } = null!;
    public string Icon => Finding.Severity switch
    {
        ReviewSeverity.Pass => "✓",
        ReviewSeverity.Fail => "✗",
        _ => "⚠",
    };
    public string Title => Finding.FromAi ? $"{Finding.Title} (AI)" : Finding.Title;
    public string Detail => Finding.Detail;
    public string SeverityKey => Finding.Severity.ToString();
}

public sealed partial class ImpactRow : ObservableObject
{
    public string Action { get; init; } = "";
    public string Basis { get; init; } = "";
    [ObservableProperty] private bool _done;
}

/// <summary>역추적·검토 — 기록의 계보, 정합성 점검, 개정 영향 체크리스트.</summary>
public partial class TraceViewModel : ObservableObject
{
    private readonly AppServices _services;
    private RecordEntry? _record;

    public ObservableCollection<LineageCard> Lineage { get; } = new();
    public ObservableCollection<FindingRow> Findings { get; } = new();
    public ObservableCollection<ImpactRow> Impacts { get; } = new();

    [ObservableProperty] private string _title = "기록을 선택하세요";
    [ObservableProperty] private string _subTitle = "기록 탐색에서 행을 더블클릭하면 이 화면이 열립니다";
    [ObservableProperty] private string _reviewStatus = "";
    [ObservableProperty] private bool _isReviewing;

    public TraceViewModel(AppServices services) => _services = services;

    public void SetRecord(RecordEntry record)
    {
        _record = record;
        Title = record.Title;
        SubTitle = $"{record.FilePath} · Rev.{record.Rev?.ToString() ?? "?"} · {record.PerformedDate:yyyy-MM-dd} · {record.Status.Label()}";
        Findings.Clear();
        ReviewStatus = "";

        Lineage.Clear();
        if (record.NodeId is not null)
        {
            foreach (var node in HierarchyService.Lineage(_services.Network, record.NodeId))
            {
                Lineage.Add(new LineageCard
                {
                    Level = NodeTypes.Label(node.Type),
                    Name = node.Name,
                    Sub = node.Rev is { Length: > 0 } ? $"{node.Id} Rev.{node.Rev}" : node.Id,
                });
            }
        }
        Lineage.Add(new LineageCard { Level = "기록", Name = record.Title, Sub = "이 문서", IsTarget = true });

        Impacts.Clear();
        foreach (var i in _services.Rules.ImpactOfRevision(record.NodeId ?? ""))
            Impacts.Add(new ImpactRow { Action = i.Action, Basis = i.Basis });
    }

    [RelayCommand]
    private async Task ReviewAsync()
    {
        if (_record is null || IsReviewing) return;
        IsReviewing = true;
        Findings.Clear();
        ReviewStatus = "규칙 기반 점검 중…";
        _services.Audit.Log("record.review", _record.FilePath);

        try
        {
            var body = await Task.Run(() => TextExtractService.TryExtract(_record.FilePath));
            var siblings = _services.Records.Where(r => r.NodeId == _record.NodeId).ToList();
            var findings = await Task.Run(() => _services.Rules.Review(_record, body, siblings));
            foreach (var f in findings) Findings.Add(new FindingRow { Finding = f });

            if (_services.Ai.IsConfigured && body is not null && _record.NodeId is not null)
            {
                ReviewStatus = "AI 대조 점검 중… (관련 절차·지침과 문안 비교)";
                var refs = BuildReferences(_record.NodeId);
                var aiFindings = await _services.Ai.ReviewAgainstReferencesAsync(_record.Title, body, refs);
                foreach (var f in aiFindings) Findings.Add(new FindingRow { Finding = f });
            }

            var fails = Findings.Count(f => f.Finding.Severity == ReviewSeverity.Fail);
            var warns = Findings.Count(f => f.Finding.Severity == ReviewSeverity.Warning);
            ReviewStatus = $"점검 완료 — {Findings.Count}개 항목 중 불일치 {fails} · 확인필요 {warns}"
                           + (_services.Ai.IsConfigured ? "" : "  (AI 미연결 — 규칙 점검만 수행)");
        }
        finally
        {
            IsReviewing = false;
        }
    }

    /// <summary>계보 상 문서들의 본문 발췌를 AI 대조용 참조로 수집한다. 폴더 바인딩에서 파일을 찾을 수 있을 때만.</summary>
    private List<(string, string, string)> BuildReferences(string nodeId)
    {
        var refs = new List<(string, string, string)>();
        var cur = nodeId;
        while (cur is not null && _services.Network.NodesById.TryGetValue(cur, out var node))
        {
            var file = _services.Records.FirstOrDefault(r =>
                r.FileName.Contains(node.Id, StringComparison.OrdinalIgnoreCase));
            var excerpt = file is not null ? TextExtractService.TryExtract(file.FilePath, 8000) : null;
            refs.Add((node.Id, node.Name, excerpt ?? $"(본문 미확보 — 등록정보: Rev.{node.Rev}, 적용일 {node.Effective})"));
            cur = HierarchyService.ParentOf(cur);
        }
        return refs;
    }

    [RelayCommand]
    private void OpenFile()
    {
        if (_record is null) return;
        _services.Audit.Log("record.openfile", _record.FilePath);
        try
        {
            Process.Start(new ProcessStartInfo(_record.FilePath) { UseShellExecute = true });
        }
        catch
        {
            ReviewStatus = "파일을 열 수 없습니다 — 경로 확인 필요";
        }
    }
}
