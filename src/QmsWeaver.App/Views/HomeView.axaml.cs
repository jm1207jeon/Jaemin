using Avalonia.Controls;
using Avalonia.Interactivity;
using QmsWeaver.App.ViewModels;

namespace QmsWeaver.App.Views;

public partial class HomeView : UserControl
{
    public HomeView() => InitializeComponent();

    private void OnTileClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string section } && DataContext is MainViewModel vm)
            vm.Section = section;
    }
}
