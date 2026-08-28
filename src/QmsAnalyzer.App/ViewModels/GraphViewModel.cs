using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using QmsAnalyzer.Core.Models;

namespace QmsAnalyzer.App.ViewModels;

public sealed class NeighborRow
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string EdgeLabel { get; init; } = "";
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

    public ObservableCollection<NeighborRow> Neighbors { get; } = new();

    /// <summary>GraphView 코드비하인드가 구독 — 컨트롤에 선택/필터 변경을 전달.</summary>
    public event Action<string>? NodeSelectionRequested;
    public event Action? FilterChanged;

    public GraphViewModel(AppServices services) => _services = services;

    partial void OnShowProceduresChanged(bool v) => FilterChanged?.Invoke();
    partial void OnShowSopsChanged(bool v) => FilterChanged?.Invoke();
    partial void OnShowFormsChanged(bool v) => FilterChanged?.Invoke();
    partial void OnShowExternalsChanged(bool v) => FilterChanged?.Invoke();
    partial void OnShowTechDocsChanged(bool v) => FilterChanged?.Invoke();

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
