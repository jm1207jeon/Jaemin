using Avalonia.Controls;
using Avalonia.Input;
using QmsAnalyzer.App.ViewModels;

namespace QmsAnalyzer.App.Views;

public partial class DrillDownView : UserControl
{
    public DrillDownView() => InitializeComponent();

    private void OnRecordDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.Drill.SelectedRecord is { } row)
            vm.Drill.OpenRecordCommand.Execute(row);
    }
}
