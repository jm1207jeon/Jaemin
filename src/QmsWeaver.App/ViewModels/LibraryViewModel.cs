using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QmsWeaver.Core.Models;

namespace QmsWeaver.App.ViewModels;

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

    /// <summary>현재 필터된 목록을 CSV로 내보낸다 (Excel용 UTF-8 BOM).</summary>
    [RelayCommand]
    private void ExportCsv()
    {
        try
        {
            var dir = Path.Combine(_services.Config.ConfigDir, "exports");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"documents-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
            var sb = new StringBuilder();
            sb.AppendLine("문서번호,문서명,구분,Rev,적용일,관리부서,기록수");
            foreach (var r in Rows)
                sb.AppendLine(string.Join(",",
                    Csv(r.Id), Csv(r.Name), Csv(r.TypeLabel), Csv(r.Rev), Csv(r.Effective), Csv(r.Dept), r.RecordCount));
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
            _services.Audit.Log("library.export", path, $"{Rows.Count} rows");
            Summary = $"CSV 내보내기 완료 → {path}";
        }
        catch (Exception ex)
        {
            Summary = $"내보내기 실패: {ex.Message}";
        }
    }

    private static string Csv(string s) =>
        s.Contains(',') || s.Contains('"') ? '"' + s.Replace("\"", "\"\"") + '"' : s;
}
