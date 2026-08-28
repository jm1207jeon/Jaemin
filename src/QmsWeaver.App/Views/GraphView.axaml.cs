using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using QmsWeaver.App.ViewModels;

namespace QmsWeaver.App.Views;

public partial class GraphView : UserControl
{
    private bool _wired;
    private MainViewModel? Vm => DataContext as MainViewModel;

    public GraphView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Wire();
        AttachedToVisualTree += (_, _) => Wire();
    }

    private void Wire()
    {
        if (_wired || Vm is null) return;
        _wired = true;
        var vm = Vm;
        Graph.Initialize(vm.Services.Network, vm.Graph.IsTypeVisible, vm.Graph.OnNodeSelected);
        vm.Graph.FilterChanged += () => Graph.RefreshFilter();
        vm.Graph.NodeSelectionRequested += id =>
        {
            Graph.SelectById(id);
            Graph.CenterOn(id);
        };
        vm.Graph.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(GraphViewModel.LayoutFrozen))
                Graph.SetFrozen(vm.Graph.LayoutFrozen);
        };
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) DoSearch();
    }

    private void OnSearchClick(object? sender, RoutedEventArgs e) => DoSearch();

    private void DoSearch()
    {
        var id = Vm?.Graph.Search();
        if (id is not null)
        {
            // 필터가 자동으로 켜졌을 수 있으므로 리빌드 후 센터링
            Graph.RefreshFilter();
            Graph.SelectById(id);
            Graph.CenterOn(id);
        }
    }

    private void OnFitClick(object? sender, RoutedEventArgs e) => Graph.FitToView();
}
