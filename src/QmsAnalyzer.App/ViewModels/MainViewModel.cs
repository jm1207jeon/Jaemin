using CommunityToolkit.Mvvm.ComponentModel;
using QmsAnalyzer.Core.Models;

namespace QmsAnalyzer.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public AppServices Services { get; }

    public LibraryViewModel Library { get; }
    public GraphViewModel Graph { get; }
    public DrillDownViewModel Drill { get; }
    public TraceViewModel Trace { get; }
    public DashboardViewModel Dashboard { get; }
    public SettingsViewModel Settings { get; }

    [ObservableProperty]
    private string _section = "library";

    public event Action<string>? SectionChanged;

    public MainViewModel(AppServices services)
    {
        Services = services;
        Library = new LibraryViewModel(services);
        Graph = new GraphViewModel(services);
        Drill = new DrillDownViewModel(services, this);
        Trace = new TraceViewModel(services);
        Dashboard = new DashboardViewModel(services);
        Settings = new SettingsViewModel(services);
    }

    partial void OnSectionChanged(string value) => SectionChanged?.Invoke(value);

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
