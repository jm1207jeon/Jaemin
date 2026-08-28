using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using QmsAnalyzer.Core.Models;

namespace QmsAnalyzer.App.ViewModels;

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

        Tiles.Clear();
        var docCount = net.Nodes.Count(n => n.Type is not (NodeTypes.Standard or NodeTypes.Regulation or NodeTypes.TechDoc));
        Tiles.Add(new StatTile { Value = docCount.ToString(), Label = "등록 문서" });
        Tiles.Add(new StatTile { Value = net.Edges.Count.ToString(), Label = "관계(엣지)" });
        Tiles.Add(new StatTile { Value = _services.Records.Count.ToString(), Label = "스캔된 기록" });
        var missing = _services.Records.Count(r => r.Status == RecordStatus.MissingApproval);
        Tiles.Add(new StatTile { Value = missing.ToString(), Label = "승인 서명 누락 기록", IsWarning = missing > 0 });

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
        foreach (var r in _services.Records.Where(r => r.Status == RecordStatus.MissingApproval).Take(20))
            Warnings.Add(new WarningRow { Title = $"승인 서명 누락 — {r.Title}", Detail = r.FilePath });

        AuditTail.Clear();
        foreach (var line in _services.Audit.Tail(15).Reverse())
            AuditTail.Add(line);
    }
}
