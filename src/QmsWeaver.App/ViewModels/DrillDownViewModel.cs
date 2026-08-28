using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QmsWeaver.Core.Models;

namespace QmsWeaver.App.ViewModels;

public sealed class DrillItem
{
    public string Key { get; init; } = "";
    public string Label { get; init; } = "";
    public int Count { get; init; }
    public string Display => $"{Label}  ({Count})";
}

public sealed class RecordRow
{
    public RecordEntry Entry { get; init; } = null!;
    public string Title => Entry.Title;
    public string Rev => Entry.Rev?.ToString() ?? "—";
    public string Date => Entry.PerformedDate?.ToString("yyyy-MM-dd") ?? "";
    public string Author => Entry.Author ?? "—";
    public string Reviewer => Entry.Reviewer ?? "—";
    public string Approver => Entry.Approver ?? "—";
    public string Status => Entry.Status.Label();
    /// <summary>개정본 상태: 최신본 = "현행", 이전 개정본 = "구버전" (조회는 계속 가능).</summary>
    public string Version => Entry.IsCurrent ? "현행" : "구버전";
    public bool IsCurrent => Entry.IsCurrent;
    /// <summary>대표 형식 + 병존 형식 (예: "DOCX (+PDF)" = docx 우선 선택됨).</summary>
    public string Format => Entry.FormatDisplay;
}

/// <summary>기록 탐색 — 좌→우 Miller Columns: 절차서 → 그룹 → 실행단위 → 기록 테이블.</summary>
public partial class DrillDownViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly MainViewModel _main;

    public ObservableCollection<DrillItem> Procedures { get; } = new();
    public ObservableCollection<DrillItem> Groups { get; } = new();
    public ObservableCollection<DrillItem> Units { get; } = new();
    public ObservableCollection<RecordRow> Records { get; } = new();

    [ObservableProperty] private DrillItem? _selectedProcedure;
    [ObservableProperty] private DrillItem? _selectedGroup;
    [ObservableProperty] private DrillItem? _selectedUnit;
    [ObservableProperty] private RecordRow? _selectedRecord;
    [ObservableProperty] private string _breadcrumb = "폴더를 연결하면 기록이 표시됩니다 (설정 → 폴더 연결)";
    [ObservableProperty] private bool _noRecords = true;

    public DrillDownViewModel(AppServices services, MainViewModel main)
    {
        _services = services;
        _main = main;
        services.RecordsChanged += RefreshProcedures;
        RefreshProcedures();
    }

    private void RefreshProcedures()
    {
        Procedures.Clear();
        var byNode = _services.Records
            .Where(r => r.NodeId is not null)
            .GroupBy(r => r.NodeId!)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var node in _services.Network.Network.Nodes
                     .Where(n => n.Type is NodeTypes.Procedure or NodeTypes.Manual)
                     .OrderBy(n => n.Id, StringComparer.Ordinal))
        {
            var count = byNode.GetValueOrDefault(node.Id);
            Procedures.Add(new DrillItem { Key = node.Id, Label = $"{node.Id} {node.Name}", Count = count });
        }
        var unmatched = _services.Records.Count(r => r.NodeId is null);
        if (unmatched > 0)
            Procedures.Insert(0, new DrillItem { Key = "__inbox__", Label = "미분류 인박스", Count = unmatched });
        NoRecords = _services.Records.Count == 0;
        RefreshGroups();
    }

    [RelayCommand]
    private void GoSettings() => _main.Section = "settings";

    partial void OnSelectedProcedureChanged(DrillItem? value) => RefreshGroups();
    partial void OnSelectedGroupChanged(DrillItem? value) => RefreshUnits();
    partial void OnSelectedUnitChanged(DrillItem? value) => RefreshRecords();

    private IEnumerable<RecordEntry> ProcRecords() =>
        SelectedProcedure is null
            ? Enumerable.Empty<RecordEntry>()
            : SelectedProcedure.Key == "__inbox__"
                ? _services.Records.Where(r => r.NodeId is null)
                : _services.Records.Where(r => r.NodeId == SelectedProcedure.Key);

    private void RefreshGroups()
    {
        Groups.Clear();
        foreach (var g in ProcRecords()
                     .GroupBy(r => r.Group ?? "(기타)")
                     .OrderByDescending(g => g.Count()))
            Groups.Add(new DrillItem { Key = g.Key, Label = g.Key, Count = g.Count() });
        SelectedGroup = Groups.FirstOrDefault();
        RefreshUnits();
    }

    private void RefreshUnits()
    {
        Units.Clear();
        if (SelectedGroup is not null)
        {
            foreach (var u in ProcRecords()
                         .Where(r => (r.Group ?? "(기타)") == SelectedGroup.Key)
                         .GroupBy(r => r.Unit ?? "(전체)")
                         .OrderByDescending(g => g.Key, StringComparer.Ordinal))
                Units.Add(new DrillItem { Key = u.Key, Label = u.Key, Count = u.Count() });
            SelectedUnit = Units.FirstOrDefault();
        }
        RefreshRecords();
    }

    private void RefreshRecords()
    {
        Records.Clear();
        if (SelectedGroup is null || SelectedUnit is null)
        {
            Breadcrumb = _services.Records.Count == 0
                ? "폴더를 연결하면 기록이 표시됩니다 (설정 → 폴더 연결)"
                : "절차서를 선택하세요";
            return;
        }
        var rows = ProcRecords()
            .Where(r => (r.Group ?? "(기타)") == SelectedGroup.Key && (r.Unit ?? "(전체)") == SelectedUnit.Key)
            .OrderBy(r => r.PerformedDate);
        foreach (var r in rows) Records.Add(new RecordRow { Entry = r });
        Breadcrumb = $"{SelectedProcedure?.Label}  ›  {SelectedGroup.Label}  ›  {SelectedUnit.Label}  ·  기록 {Records.Count}건";
    }

    [RelayCommand]
    private void OpenRecord(RecordRow? row)
    {
        if (row is null) return;
        _services.Audit.Log("record.open", row.Entry.FilePath);
        _main.ShowTrace(row.Entry);
    }
}
