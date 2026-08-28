using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using QmsAnalyzer.Core.Models;

namespace QmsAnalyzer.App.ViewModels;

public sealed class DocRow
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string TypeLabel { get; init; } = "";
    public string Rev { get; init; } = "";
    public string Effective { get; init; } = "";
    public string Dept { get; init; } = "";
    public int RecordCount { get; init; }
}

public partial class LibraryViewModel : ObservableObject
{
    private readonly AppServices _services;

    public ObservableCollection<DocRow> Rows { get; } = new();
    public List<string> TypeFilters { get; } = new()
    {
        "전체", "품질매뉴얼", "절차서", "지침서", "작업표준서", "양식", "규격", "규제", "기술문서",
    };

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _selectedType = "전체";
    [ObservableProperty] private string _summary = "";

    public LibraryViewModel(AppServices services)
    {
        _services = services;
        services.RecordsChanged += Refresh;
        Refresh();
    }

    partial void OnSearchTextChanged(string value) => Refresh();
    partial void OnSelectedTypeChanged(string value) => Refresh();

    private void Refresh()
    {
        Rows.Clear();
        var q = SearchText.Trim();
        var docs = _services.Network.Network.Nodes
            .Where(n => SelectedType == "전체" || NodeTypes.Label(n.Type) == SelectedType)
            .Where(n => q.Length == 0
                        || n.Id.Contains(q, StringComparison.OrdinalIgnoreCase)
                        || n.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                        || (n.NameEn?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderBy(n => n.Id, StringComparer.Ordinal);

        foreach (var n in docs)
        {
            Rows.Add(new DocRow
            {
                Id = n.Id,
                Name = n.Name,
                TypeLabel = NodeTypes.Label(n.Type),
                Rev = n.Rev ?? "",
                Effective = n.Effective ?? "",
                Dept = n.Dept ?? "",
                RecordCount = _services.RecordCount(n.Id),
            });
        }
        Summary = $"문서 {Rows.Count}건 · 기록 {_services.Records.Count}건";
    }
}
