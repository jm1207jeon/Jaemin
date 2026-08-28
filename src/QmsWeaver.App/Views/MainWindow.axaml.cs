using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Styling;
using QmsWeaver.App.ViewModels;

namespace QmsWeaver.App.Views;

public partial class MainWindow : Window
{
    private readonly Dictionary<string, Control> _views = new();
    private MainViewModel? Vm => DataContext as MainViewModel;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (Vm is null) return;
            _views.Clear();
            _views["home"] = new HomeView { DataContext = Vm };
            _views["library"] = new LibraryView { DataContext = Vm };
            _views["graph"] = new GraphView { DataContext = Vm };
            _views["drill"] = new DrillDownView { DataContext = Vm };
            _views["trace"] = new TraceView { DataContext = Vm };
            _views["dashboard"] = new DashboardView { DataContext = Vm };
            _views["settings"] = new SettingsView { DataContext = Vm };
            Vm.SectionChanged += ShowSection;
            ShowSection(Vm.Section);
            UpdateAiBadge();
        };
    }

    private void ShowSection(string section)
    {
        if (_views.TryGetValue(section, out var view))
            Host.Content = view;
        if (section == "dashboard" && Vm is not null)
            Vm.Dashboard.Refresh();
        UpdateAiBadge();
        // 네비게이션 체크 상태 동기화 (코드에서 화면 전환된 경우)
        foreach (var rb in this.GetLogicalDescendants().OfType<RadioButton>())
        {
            if (rb.Tag is string tag && tag == section && rb.IsChecked != true)
                rb.IsChecked = true;
        }
    }

    private void UpdateAiBadge()
    {
        if (Vm is null) return;
        AiBadge.Text = Vm.Services.Ai.IsConfigured
            ? $"AI 연결됨 · {Vm.Services.Config.Config.Ai.Model}"
            : "AI 미연결 (로컬 분석)";
    }

    private void OnNavChecked(object? sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { IsChecked: true, Tag: string tag } && Vm is not null && Vm.Section != tag)
            Vm.Section = tag;
    }

    private void OnGlobalSearchKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Enter && Vm is not null)
        {
            Vm.GlobalSearch(GlobalSearch.Text ?? "");
            GlobalSearch.Text = "";
        }
    }

    private void OnThemeToggle(object? sender, RoutedEventArgs e)
    {
        var app = Avalonia.Application.Current;
        if (app is null) return;
        app.RequestedThemeVariant = app.ActualThemeVariant == ThemeVariant.Dark
            ? ThemeVariant.Light
            : ThemeVariant.Dark;
    }
}
