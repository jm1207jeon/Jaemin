using CommunityToolkit.Mvvm.ComponentModel;
using QmsWeaver.Core.Models;

namespace QmsWeaver.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public AppServices Services { get; }

    public HomeViewModel Home { get; }
    public LibraryViewModel Library { get; }
    public GraphViewModel Graph { get; }
    public DrillDownViewModel Drill { get; }
    public TraceViewModel Trace { get; }
    public DashboardViewModel Dashboard { get; }
    public SettingsViewModel Settings { get; }

    [ObservableProperty]
    private string _section = "home";

    [ObservableProperty]
    private string _statusBarText = "";

    public event Action<string>? SectionChanged;

    public MainViewModel(AppServices services)
    {
        Services = services;
        Home = new HomeViewModel(services);
        Library = new LibraryViewModel(services);
        Graph = new GraphViewModel(services);
        Drill = new DrillDownViewModel(services, this);
        Trace = new TraceViewModel(services);
        Dashboard = new DashboardViewModel(services);
        Settings = new SettingsViewModel(services);
        services.RecordsChanged += RefreshStatusBar;
        RefreshStatusBar();
    }

    partial void OnSectionChanged(string value)
    {
        SectionChanged?.Invoke(value);
        if (value == "home") Home.Refresh();
        RefreshStatusBar();
    }

    public void RefreshStatusBar()
    {
        var folders = Services.Config.Config.FolderBindings.Count;
        var missing = Services.Records.Count(r => r.Status == Core.Models.RecordStatus.MissingApproval);
        var docCount = Services.Network.Network.Nodes.Count(n =>
            n.Type is not (Core.Models.NodeTypes.Standard or Core.Models.NodeTypes.Regulation
                or Core.Models.NodeTypes.TechDoc));
        StatusBarText = $"등록 문서 {docCount}  ·  기록 {Services.Records.Count}"
                        + $"  ·  폴더 {folders}개 연결"
                        + (missing > 0 ? $"  ·  ⚠ 서명 누락 {missing}" : "")
                        + (Services.Ai.IsConfigured ? $"  ·  AI: {Services.Config.Config.Ai.Model}" : "  ·  AI 미연결");
    }

    /// <summary>헤더 전역 검색: 문서 매칭 시 그래프에서 위치 표시, 아니면 라이브러리 필터.</summary>
    public void GlobalSearch(string query)
    {
        query = query.Trim();
        if (query.Length == 0) return;
        Services.Audit.Log("search", query);
        var node = Services.Network.Network.Nodes.FirstOrDefault(n =>
            n.Id.Equals(query, StringComparison.OrdinalIgnoreCase) ||
            n.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            n.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
        if (node is not null)
        {
            Graph.SearchText = query;
            Section = "graph";
            Graph.Search();
        }
        else
        {
            Library.SearchText = query;
            Section = "library";
        }
    }

    /// <summary>드릴다운·라이브러리에서 기록을 열면 역추적 화면으로 전환.</summary>
    public void ShowTrace(RecordEntry record)
    {
        Trace.SetRecord(record);
        Section = "trace";
    }

    public void ShowGraphNode(string nodeId)
    {
        Graph.SelectNodeById(nodeId);
        Section = "graph";
    }
}
