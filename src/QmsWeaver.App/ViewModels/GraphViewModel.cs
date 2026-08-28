using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using QmsWeaver.Core.Models;

namespace QmsWeaver.App.ViewModels;

public sealed class NeighborRow
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string EdgeLabel { get; init; } = "";
}

public sealed class LegendItem
{
    public string Label { get; init; } = "";
    public int Count { get; init; }
    public string ColorKey { get; init; } = "";
    public string Display => Count > 0 ? $"{Label} {Count}" : Label;
}

/// <summary>형상 범례 — 문서 성격별 노드 모양 (색과 독립적인 2차 채널).</summary>
public sealed class ShapeLegendItem
{
    public string TypeKey { get; init; } = "";
    public string Label { get; init; } = "";
}

public partial class GraphViewModel : ObservableObject
{
    private readonly AppServices _services;

    [ObservableProperty] private string _selectedTitle = "노드를 클릭하면 상세가 표시됩니다";
    [ObservableProperty] private string _selectedSub = "규제·규격 노드를 클릭하면 요구 문서가 강조됩니다";
    [ObservableProperty] private string _selectedId = "";

    // 레이어 토글 (기본: 코어 — 양식/지침 숨김)
    [ObservableProperty] private bool _showProcedures = true;
    [ObservableProperty] private bool _showSops;
    [ObservableProperty] private bool _showForms;
    [ObservableProperty] private bool _showExternals = true;
    [ObservableProperty] private bool _showTechDocs = true;

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _searchStatus = "";
    [ObservableProperty] private bool _layoutFrozen;

    public ObservableCollection<NeighborRow> Neighbors { get; } = new();
    public ObservableCollection<LegendItem> Legend { get; } = new();

    /// <summary>형상 범례 (고정) — 매뉴얼 ◎ · 절차서 ● · 지침 ▢ · 양식 ◆ · 규격 ▲ · 규제 ▼ · 기술문서 ⬢</summary>
    public List<ShapeLegendItem> ShapeLegend { get; } = new()
    {
        new() { TypeKey = NodeTypes.Manual, Label = "매뉴얼" },
        new() { TypeKey = NodeTypes.Procedure, Label = "절차서" },
        new() { TypeKey = NodeTypes.Sop, Label = "지침·작업표준" },
        new() { TypeKey = NodeTypes.Form, Label = "양식" },
        new() { TypeKey = NodeTypes.Standard, Label = "규격" },
        new() { TypeKey = NodeTypes.Regulation, Label = "규제" },
        new() { TypeKey = NodeTypes.TechDoc, Label = "기술문서" },
    };

    /// <summary>GraphView 코드비하인드가 구독 — 컨트롤에 선택/필터 변경을 전달.</summary>
    public event Action<string>? NodeSelectionRequested;
    public event Action? FilterChanged;

    public GraphViewModel(AppServices services)
    {
        _services = services;
        RefreshLegend();
    }

    /// <summary>색 범례 = ISO 13485 조항 대분류 (+외부 계열). 카운트는 현재 보이는 노드 기준.</summary>
    public void RefreshLegend()
    {
        Legend.Clear();
        var visible = _services.Network.Network.Nodes.Where(n => IsTypeVisible(n.Type)).ToList();
        int ClauseCount(int c) => visible.Count(n =>
            n.Clause == c && n.Type is NodeTypes.Procedure or NodeTypes.Sop
                or NodeTypes.WorkStandard or NodeTypes.Form);
        foreach (var (clause, label, key) in new[]
        {
            (4, "4장 시스템·문서", "Clause4Color"),
            (5, "5장 경영책임", "Clause5Color"),
            (6, "6장 자원관리", "Clause6Color"),
            (7, "7장 제품실현", "Clause7Color"),
            (8, "8장 측정·개선", "Clause8Color"),
        })
        {
            var c = ClauseCount(clause);
            if (c > 0) Legend.Add(new LegendItem { Label = label, Count = c, ColorKey = key });
        }
        void AddExternal(string type, string label, string key)
        {
            var c = visible.Count(n => n.Type == type);
            if (c > 0) Legend.Add(new LegendItem { Label = label, Count = c, ColorKey = key });
        }
        AddExternal(NodeTypes.Standard, "규격", "NodeStandardColor");
        AddExternal(NodeTypes.Regulation, "규제", "NodeRegulationColor");
        AddExternal(NodeTypes.TechDoc, "기술문서", "NodeTechDocColor");
    }

    /// <summary>검색: 문서번호/이름 매칭 노드를 찾아 선택·센터링 요청. 결과 노드 ID 반환.</summary>
    public string? Search()
    {
        var q = SearchText.Trim();
        if (q.Length == 0) { SearchStatus = ""; return null; }
        var node = _services.Network.Network.Nodes
                       .FirstOrDefault(n => n.Id.Equals(q, StringComparison.OrdinalIgnoreCase))
                   ?? _services.Network.Network.Nodes
                       .FirstOrDefault(n => IsTypeVisible(n.Type) &&
                                            (n.Id.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                                             n.Name.Contains(q, StringComparison.OrdinalIgnoreCase)))
                   ?? _services.Network.Network.Nodes
                       .FirstOrDefault(n => n.Id.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                                            n.Name.Contains(q, StringComparison.OrdinalIgnoreCase));
        if (node is null)
        {
            SearchStatus = $"'{q}' — 일치 항목 없음";
            return null;
        }
        if (!IsTypeVisible(node.Type))
        {
            // 오류 방지: 숨겨진 레이어의 노드면 해당 레이어를 자동으로 켠다
            switch (node.Type)
            {
                case NodeTypes.Sop or NodeTypes.WorkStandard: ShowSops = true; break;
                case NodeTypes.Form: ShowForms = true; break;
                case NodeTypes.Standard or NodeTypes.Regulation: ShowExternals = true; break;
                case NodeTypes.TechDoc: ShowTechDocs = true; break;
                case NodeTypes.Procedure: ShowProcedures = true; break;
            }
        }
        SearchStatus = $"{node.Id} {node.Name}";
        NodeSelectionRequested?.Invoke(node.Id);
        return node.Id;
    }

    partial void OnShowProceduresChanged(bool value) => OnFilterChanged();
    partial void OnShowSopsChanged(bool value) => OnFilterChanged();
    partial void OnShowFormsChanged(bool value) => OnFilterChanged();
    partial void OnShowExternalsChanged(bool value) => OnFilterChanged();
    partial void OnShowTechDocsChanged(bool value) => OnFilterChanged();

    private void OnFilterChanged()
    {
        RefreshLegend();
        FilterChanged?.Invoke();
    }

    public bool IsTypeVisible(string type) => type switch
    {
        NodeTypes.Manual => true,
        NodeTypes.Procedure => ShowProcedures,
        NodeTypes.Sop or NodeTypes.WorkStandard => ShowSops,
        NodeTypes.Form => ShowForms,
        NodeTypes.Standard or NodeTypes.Regulation => ShowExternals,
        NodeTypes.TechDoc => ShowTechDocs,
        _ => true,
    };

    public void SelectNodeById(string nodeId) => NodeSelectionRequested?.Invoke(nodeId);

    /// <summary>그래프 컨트롤에서 노드가 선택되면 호출된다.</summary>
    public void OnNodeSelected(DocNode? node)
    {
        Neighbors.Clear();
        if (node is null)
        {
            SelectedTitle = "노드를 클릭하면 상세가 표시됩니다";
            SelectedSub = "규제·규격 노드를 클릭하면 요구 문서가 강조됩니다";
            SelectedId = "";
            return;
        }

        SelectedId = node.Id;
        SelectedTitle = node.Name;
        var meta = new List<string> { NodeTypes.Label(node.Type) };
        if (!string.IsNullOrEmpty(node.Rev)) meta.Add($"Rev.{node.Rev}");
        if (!string.IsNullOrEmpty(node.Effective)) meta.Add($"적용 {node.Effective}");
        if (!string.IsNullOrEmpty(node.Dept)) meta.Add(node.Dept!);
        if (!string.IsNullOrEmpty(node.Desc)) meta.Add(node.Desc!);
        var records = _services.RecordCount(node.Id);
        if (records > 0) meta.Add($"기록 {records}건");
        SelectedSub = $"{node.Id} · " + string.Join(" · ", meta);

        foreach (var (other, edge) in _services.Network.Neighbors(node.Id)
                     .OrderByDescending(x => x.Edge.Strength))
        {
            Neighbors.Add(new NeighborRow
            {
                Id = other.Id,
                Name = other.Name,
                EdgeLabel = EdgeTypes.Label(edge.Type) + (string.IsNullOrEmpty(edge.Label) ? "" : $" · {edge.Label}"),
            });
        }
        _services.Audit.Log("graph.select", node.Id);
    }
}
