using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using QmsWeaver.App;
using QmsWeaver.App.Tests;
using QmsWeaver.App.ViewModels;
using QmsWeaver.App.Views;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace QmsWeaver.App.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

public class UiSmokeTests
{
    private static string ShotDir
    {
        get
        {
            var dir = Environment.GetEnvironmentVariable("QMS_SHOT_DIR")
                      ?? Path.Combine(Path.GetTempPath(), "qms-shots");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private static (MainWindow Window, MainViewModel Vm) CreateWindow()
    {
        var services = new AppServices(
            Path.Combine(Path.GetTempPath(), "qmsw-cfg-" + Guid.NewGuid().ToString("N")));
        var vm = new MainViewModel(services);
        var window = new MainWindow { DataContext = vm };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, vm);
    }

    private static void PumpUntil(Func<bool> condition, int timeoutMs = 10000)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (!condition() && Environment.TickCount64 < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(15);
        }
        Assert.True(condition(), "PumpUntil 타임아웃");
    }

    private static void Capture(MainWindow window, string name)
    {
        Dispatcher.UIThread.RunJobs();
        var frame = window.CaptureRenderedFrame();
        frame?.Save(Path.Combine(ShotDir, name + ".png"));
    }

    [AvaloniaFact]
    public void AllSections_RenderWithoutBindingCrash()
    {
        var (window, vm) = CreateWindow();
        foreach (var section in new[] { "home", "library", "graph", "drill", "trace", "dashboard", "settings" })
        {
            vm.Section = section;
            Dispatcher.UIThread.RunJobs();
            Capture(window, section);
        }
        window.Close();
    }

    [AvaloniaFact]
    public void DrillDown_WithScannedRecords_ShowsRecordsAndTrace()
    {
        // 임시 폴더에 샘플 기록 구성
        var root = Path.Combine(Path.GetTempPath(), "qms-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "열처리", "2025"));
        File.WriteAllText(Path.Combine(root, "열처리", "2025", "PQ보고서_QP-706_Rev.1_2025-05-20.txt"),
            "본 보고서는 QP-706 Rev.3 및 SOP-705-03에 따라 수행되었다.");
        File.WriteAllText(Path.Combine(root, "열처리", "2025", "일탈보고서 F706-01_2025-04-28.txt"), "deviation");

        try
        {
            var (window, vm) = CreateWindow();
            vm.Services.Config.Config.FolderBindings.Add(new Core.Models.FolderBinding { Path = root });
            // 블로킹 대기는 헤드리스 디스패처와 데드락 → 완료까지 디스패처 펌핑
            var scan = vm.Services.RescanAsync();
            PumpUntil(() => scan.IsCompleted);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(2, vm.Services.Records.Count);

            vm.Section = "drill";
            Dispatcher.UIThread.RunJobs();
            var proc = vm.Drill.Procedures.First(p => p.Key == "QP-706");
            Assert.Equal(2, proc.Count);
            vm.Drill.SelectedProcedure = proc;
            Dispatcher.UIThread.RunJobs();
            Assert.NotEmpty(vm.Drill.Records);
            Capture(window, "drill-with-records");

            // 기록 열기 → 역추적 화면
            var row = vm.Drill.Records.First(r => r.Title.Contains("PQ"));
            vm.Drill.OpenRecordCommand.Execute(row);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("trace", vm.Section);
            Assert.True(vm.Trace.Lineage.Count >= 3); // 규격/매뉴얼/절차/기록

            // 검토 실행 (AI 미연결 → 규칙 기반만)
            vm.Trace.ReviewCommand.Execute(null);
            PumpUntil(() => !vm.Trace.IsReviewing && vm.Trace.Findings.Count > 0);
            Dispatcher.UIThread.RunJobs();
            Assert.NotEmpty(vm.Trace.Findings);
            // 본문의 "QP-706 Rev.3" 인용은 현행 Rev(21)과 불일치 → Fail이 잡혀야 함
            Assert.Contains(vm.Trace.Findings, f => f.Icon == "✗");
            Assert.NotEmpty(vm.Trace.Impacts);
            Capture(window, "trace-with-review");
            window.Close();
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
