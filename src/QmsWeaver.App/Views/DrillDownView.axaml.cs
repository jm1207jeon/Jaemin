using Avalonia.Controls;
using Avalonia.Input;
using QmsWeaver.App.ViewModels;

namespace QmsWeaver.App.Views;

public partial class DrillDownView : UserControl
{
    public DrillDownView()
    {
        InitializeComponent();
        // 구버전 개정본 행은 톤 다운 — 현행본이 한눈에 구분된다
        RecordsGrid.LoadingRow += (_, e) =>
            e.Row.Opacity = (e.Row.DataContext as RecordRow)?.IsCurrent == false ? 0.55 : 1.0;
    }

    private void OnRecordDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.Drill.SelectedRecord is { } row)
            vm.Drill.OpenRecordCommand.Execute(row);
    }
}
