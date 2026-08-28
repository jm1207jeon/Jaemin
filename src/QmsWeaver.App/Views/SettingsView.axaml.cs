using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using QmsWeaver.App.ViewModels;

namespace QmsWeaver.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();

    private async void OnBrowseFolder(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        var top = TopLevel.GetTopLevel(this);
        if (top is null) return;
        var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "연결할 폴더 선택",
            AllowMultiple = false,
        });
        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (path is not null) vm.Settings.SetPickedFolder(path);
    }
}
