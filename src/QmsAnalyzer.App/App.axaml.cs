using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using QmsAnalyzer.App.ViewModels;
using QmsAnalyzer.App.Views;

namespace QmsAnalyzer.App;

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
