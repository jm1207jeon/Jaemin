using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace QmsWeaver.App.Views;

public partial class OnboardingDialog : Window
{
    /// <summary>사용자가 선택한 폴더 경로. null = 나중에.</summary>
    public string? PickedFolder { get; private set; }

    public OnboardingDialog() => InitializeComponent();

    private void OnLater(object? sender, RoutedEventArgs e) => Close();

    private async void OnPickFolder(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "QMS 문서·기록 폴더 선택",
            AllowMultiple = false,
        });
        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (path is not null)
        {
            PickedFolder = path;
            Close();
        }
        // 취소 시 다이얼로그 유지 — 다시 선택하거나 [나중에] 가능
    }
}
