using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using QmsWeaver.App.ViewModels;
using QmsWeaver.App.Views;

namespace QmsWeaver.App;

public class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = new AppServices();
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(services),
            };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
