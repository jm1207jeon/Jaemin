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
        Opened += async (_, _) => await ShowOnboardingIfFirstRunAsync();
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
            Vm.Settings.UiPrefsChanged += () =>
            {
                FontSize = Vm.Services.Config.Config.UiFontSize;
                if (Avalonia.Application.Current is { } app)
                    app.Resources["TableRowHeight"] = Vm.Services.Config.Config.TableRowHeight;
            };
            ShowSection(Vm.Section);
            UpdateAiBadge();
        };
    }

    /// <summary>
    /// 최초 실행: ISO 13485 기본 구조 안내 + QMS 폴더 연결 제안.
    /// 예 → 네이티브 폴더 선택 → 바인딩 저장 → 즉시 스캔 → 기록 탐색으로 이동.
    /// </summary>
    private async Task ShowOnboardingIfFirstRunAsync()
    {
        if (Vm is null || Vm.Services.Config.Config.OnboardingShown) return;
        Vm.Services.Config.Config.OnboardingShown = true;
        Vm.Services.Config.Save();

        var dialog = new OnboardingDialog();
        await dialog.ShowDialog(this);

        if (dialog.PickedFolder is { } path)
        {
            Vm.Services.Config.Config.FolderBindings.Add(
                new Core.Models.FolderBinding { Path = path });
            Vm.Services.Config.Save();
            Vm.Services.Audit.Log("onboarding.folder", path);
            Vm.Section = "drill"; // 스캔 진행과 결과가 바로 보이는 화면으로
            _ = Vm.Services.RescanAsync();
        }
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
