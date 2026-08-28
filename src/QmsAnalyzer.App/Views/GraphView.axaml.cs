using Avalonia.Controls;
using QmsAnalyzer.App.ViewModels;

namespace QmsAnalyzer.App.Views;

public partial class GraphView : UserControl
{
    private bool _wired;

    public GraphView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Wire();
        AttachedToVisualTree += (_, _) => Wire();
    }

    private void Wire()
    {
        if (_wired || DataContext is not MainViewModel vm) return;
        _wired = true;
        Graph.Initialize(vm.Services.Network, vm.Graph.IsTypeVisible, vm.Graph.OnNodeSelected);
        vm.Graph.FilterChanged += () => Graph.RefreshFilter();
        vm.Graph.NodeSelectionRequested += id => Graph.SelectById(id);
    }
}
