using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using QmsWeaver.Core.Models;

namespace QmsWeaver.App.ViewModels;

public sealed class StatTile
{
    public string Value { get; init; } = "";
    public string Label { get; init; } = "";
    public bool IsWarning { get; init; }
}

public sealed class BarRow
{
    public string Label { get; init; } = "";
    public int Count { get; init; }
    public double Width { get; init; } // 0~220 px
}

public sealed class WarningRow
{
    public string Title { get; init; } = "";
    public string Detail { get; init; } = "";
}

/// <summary>심사 대비 대시보드 — 분포·규제 커버리지·무결성 경고·감사 추적.</summary>
public partial class DashboardViewModel : ObservableObject
{
    private readonly AppServices _services;

    public ObservableCollection<StatTile> Tiles { get; } = new();
    public ObservableCollection<BarRow> TypeBars { get; } = new();
    public ObservableCollection<BarRow> CoverageBars { get; } = new();
    public ObservableCollection<WarningRow> Warnings { get; } = new();
    public ObservableCollection<string> AuditTail { get; } = new();

    public DashboardViewModel(AppServices services)
    {
        _services = services;
        services.RecordsChanged += Refresh;
        Refresh();
    }

    public void Refresh()
    {
        var net = _services.Network.Network;

        // EDMS 정기검토: 적용일로부터 3년 경과(또는 임박) 문서
        var reviewDue = net.Nodes
            .Where(n => n.Type is NodeTypes.Manual or NodeTypes.Procedure or NodeTypes.Sop
                        or NodeTypes.WorkStandard or NodeTypes.Form)
            .Where(n => DateOnly.TryParse(n.Effective, out var d) &&
                        d.AddYears(3) <= DateOnly.FromDateTime(DateTime.Today))
            .OrderBy(n => n.Effective, StringComparer.Ordinal)
            .ToList();

        Tiles.Clear();
        var docCount = net.Nodes.Count(n => n.Type is not (NodeTypes.Standard or NodeTypes.Regulation or NodeTypes.TechDoc));
        Tiles.Add(new StatTile { Value = docCount.ToString(), Label = "등록 문서" });
        Tiles.Add(new StatTile { Value = _services.Records.Count.ToString(), Label = "스캔된 기록" });
        var missing = _services.Records.Count(r => r.Status == RecordStatus.MissingApproval);
        Tiles.Add(new StatTile { Value = missing.ToString(), Label = "승인 서명 누락 기록", IsWarning = missing > 0 });
        Tiles.Add(new StatTile { Value = reviewDue.Count.ToString(), Label = "정기검토 도래 (적용 3년 경과)", IsWarning = reviewDue.Count > 0 });

        TypeBars.Clear();
        var typeCounts = net.Nodes
            .GroupBy(n => n.Type)
            .OrderByDescending(g => g.Count())
            .ToList();
        var max = Math.Max(1, typeCounts.Max(g => g.Count()));
        foreach (var g in typeCounts)
            TypeBars.Add(new BarRow
            {
                Label = NodeTypes.Label(g.Key),
                Count = g.Count(),
                Width = 220.0 * g.Count() / max,
            });

        CoverageBars.Clear();
        var regs = net.Nodes.Where(n => n.Type is NodeTypes.Regulation or NodeTypes.Standard).ToList();
        var covMax = Math.Max(1, regs.Max(r => _services.Network.Degree(r.Id)));
        foreach (var r in regs.OrderByDescending(r => _services.Network.Degree(r.Id)).Take(10))
            CoverageBars.Add(new BarRow
            {
                Label = r.Name,
                Count = _services.Network.Degree(r.Id),
                Width = 220.0 * _services.Network.Degree(r.Id) / covMax,
            });

        Warnings.Clear();
        foreach (var gap in net.Nodes.Where(n => n.Gap == true))
            Warnings.Add(new WarningRow { Title = $"갭 후보 — {gap.Name}", Detail = gap.Src ?? "전담 문서 미확인" });
        foreach (var d in reviewDue.Take(15))
            Warnings.Add(new WarningRow
            {
                Title = $"정기검토 도래 — {d.Id} {d.Name}",
                Detail = $"적용일 {d.Effective} · Rev.{d.Rev} — 3년 경과, 유효성 검토 필요 (QP-401)",
            });
        foreach (var r in _services.Records.Where(r => r.Status == RecordStatus.MissingApproval).Take(20))
            Warnings.Add(new WarningRow { Title = $"승인 서명 누락 — {r.Title}", Detail = r.FilePath });

        AuditTail.Clear();
        foreach (var line in _services.Audit.Tail(15).Reverse())
            AuditTail.Add(line);
    }
}
