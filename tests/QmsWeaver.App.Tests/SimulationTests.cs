using System.Text;
using Avalonia.Threading;
using QmsWeaver.App.ViewModels;
using QmsWeaver.App.Views;
using QmsWeaver.Core.Models;
using Avalonia.Headless.XUnit;
using Xunit;
using Xunit.Abstractions;

namespace QmsWeaver.App.Tests;

/// <summary>
/// 다부서·다직급 사용 시나리오 시뮬레이션.
/// 페르소나(QA/QC/QMR/품질팀장/RA/MA/개발) × 과업 프로필 × 데이터 변형(폴더없음/정상/지저분함/대규모)
/// 조합으로 320개 여정(1,000+ 인터랙션)을 실행하며 UI 불변식을 검증하고 마찰(friction) 로그를 남긴다.
/// </summary>
public class SimulationTests
{
    private readonly ITestOutputHelper _out;
    public SimulationTests(ITestOutputHelper output) => _out = output;

    private sealed record Persona(string Name, string[] Tasks);

    private static readonly Persona[] Personas =
    {
        new("QA담당", new[] { "find", "drill", "review", "global", "export" }),
        new("QC검사원", new[] { "drill", "review", "find" }),
        new("QMR", new[] { "dashboard", "graph", "review", "global" }),
        new("품질팀장", new[] { "dashboard", "export", "global", "graph" }),
        new("RA담당", new[] { "find", "graphsearch", "global", "export" }),
        new("MA담당", new[] { "find", "drill", "global" }),
        new("개발엔지니어", new[] { "graph", "graphsearch", "find", "review" }),
    };

    private sealed class Friction
    {
        public readonly List<string> Entries = new();
        public int Journeys;
        public int Interactions;
        public void Log(string persona, string task, string note) =>
            Entries.Add($"[{persona}/{task}] {note}");
    }

    private static void Pump(int ms = 0)
    {
        Dispatcher.UIThread.RunJobs();
        if (ms > 0) { Thread.Sleep(ms); Dispatcher.UIThread.RunJobs(); }
    }

    private static void PumpUntil(Func<bool> condition, int timeoutMs = 15000)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (!condition() && Environment.TickCount64 < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }
        Assert.True(condition(), "PumpUntil 타임아웃");
    }

    // ── 데이터 변형 생성 ────────────────────────────────────────────
    private static string MakeVariant(string kind)
    {
        var root = Path.Combine(Path.GetTempPath(), $"qmsw-sim-{kind}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        switch (kind)
        {
            case "none":
                break;
            case "clean":
                foreach (var (proc, grp, unit, files) in new[]
                {
                    ("QP-706", "열처리", "2025", new[] { "VP계획서_QP-706_Rev.1_2025-03-02", "IQ보고서_Rev.0_2025-03-15", "PQ보고서_QP-706_Rev.1_2025-05-20" }),
                    ("QP-706", "열처리", "2024", new[] { "PQ보고서_QP-706_Rev.0_2024-05-11" }),
                    ("QP-802", "고객불만", "2025", new[] { "고객불만처리보고서_F802-02_2025-06-01", "Case_Analysis_F802-15_2025-06-11" }),
                    ("QP-601", "교육", "2025", new[] { "교육결과보고서_F601-03_2025-01-20" }),
                })
                {
                    var dir = Path.Combine(root, grp, unit);
                    Directory.CreateDirectory(dir);
                    foreach (var f in files)
                        File.WriteAllText(Path.Combine(dir, f + ".txt"),
                            $"본 기록은 {proc} Rev.2 및 SOP-705-03에 따라 수행되었다.");
                }
                break;
            case "messy":
                Directory.CreateDirectory(Path.Combine(root, "옛날자료", "백업", "임시"));
                File.WriteAllText(Path.Combine(root, "메모.txt"), "문서번호 없음");
                File.WriteAllText(Path.Combine(root, "회의록 복사본 (2).txt"), "그냥 회의록");
                File.WriteAllText(Path.Combine(root, "~$temp.docx"), "office lock");
                File.WriteAllText(Path.Combine(root, "옛날자료", "백업", "임시", "출하검사성적서 SOP-805-12.txt"), "출하검사");
                File.WriteAllText(Path.Combine(root, "밸리데이션보고서_QP-706.txt"), "날짜와 Rev가 없는 파일");
                File.WriteAllText(Path.Combine(root, "스캔본_20250101.pdf"), "%PDF-fake");
                break;
            case "large":
                for (var p = 0; p < 8; p++)
                {
                    var proc = $"QP-70{p + 1}";
                    for (var g = 0; g < 3; g++)
                    {
                        var dir = Path.Combine(root, $"그룹{g}", $"202{3 + g}");
                        Directory.CreateDirectory(dir);
                        for (var i = 0; i < 12; i++)
                            File.WriteAllText(Path.Combine(dir, $"기록_{proc}_Rev.{i % 4}_202{3 + g}-0{i % 9 + 1}-1{i % 9}.txt"),
                                $"{proc}에 따른 기록 {i}");
                    }
                }
                break;
        }
        return root;
    }

    [AvaloniaFact]
    public void MultiRoleScenarioSimulation_320Journeys()
    {
        var friction = new Friction();
        var report = new StringBuilder();
        var variants = new (string Kind, int Journeys)[]
            { ("none", 60), ("clean", 100), ("messy", 100), ("large", 60) };
        var seed = 20260828;

        foreach (var (kind, journeyCount) in variants)
        {
            var root = MakeVariant(kind);
            var configDir = Path.Combine(Path.GetTempPath(), $"qmsw-cfg-{Guid.NewGuid():N}");
            try
            {
                var services = new AppServices(configDir);
                services.Config.Config.OnboardingShown = true;
                var vm = new MainViewModel(services);
                // 주의: Show() 하지 않음 — VM·화면전환 로직 검증에 렌더링은 불필요하며,
                // CI의 소프트웨어 렌더러에서 320개 여정 × 매 프레임 렌더가 45분 병목이 됐음.
                var window = new MainWindow { DataContext = vm };
                Pump();

                if (kind != "none")
                {
                    services.Config.Config.FolderBindings.Add(new FolderBinding { Path = root });
                    var scan = services.RescanAsync();
                    PumpUntil(() => scan.IsCompleted);
                    Pump();
                }

                var rng = new Random(seed++);
                for (var j = 0; j < journeyCount; j++)
                {
                    var persona = Personas[rng.Next(Personas.Length)];
                    var steps = rng.Next(3, 6);
                    friction.Journeys++;
                    for (var s = 0; s < steps; s++)
                    {
                        var task = persona.Tasks[rng.Next(persona.Tasks.Length)];
                        friction.Interactions++;
                        RunTask(task, vm, services, rng, kind, persona.Name, friction);
                    }
                }

                Pump();
            }
            finally
            {
                try { Directory.Delete(root, true); } catch { }
                try { Directory.Delete(configDir, true); } catch { }
            }
        }

        report.AppendLine($"여정 {friction.Journeys}회 · 인터랙션 {friction.Interactions}회");
        report.AppendLine($"마찰 로그 {friction.Entries.Count}건:");
        foreach (var grp in friction.Entries.GroupBy(x => x).OrderByDescending(g => g.Count()).Take(40))
            report.AppendLine($"  x{grp.Count(),-4} {grp.Key}");
        var reportText = report.ToString();
        _out.WriteLine(reportText);
        var dir = Environment.GetEnvironmentVariable("QMS_SHOT_DIR");
        if (dir is not null)
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "simulation-report.txt"), reportText);
        }

        Assert.True(friction.Journeys >= 320, $"여정 수 부족: {friction.Journeys}");
    }

    private void RunTask(string task, MainViewModel vm, AppServices services, Random rng,
        string kind, string persona, Friction friction)
    {
        var nodes = services.Network.Network.Nodes;
        switch (task)
        {
            case "find":
            {
                vm.Section = "library"; Pump();
                var target = nodes[rng.Next(nodes.Count)];
                var query = target.Name.Length > 4 ? target.Name[..3] : target.Name;
                vm.Library.SearchText = query; Pump();
                if (vm.Library.Rows.Count == 0)
                    friction.Log(persona, task, $"검색 0건: '{query}'");
                vm.Library.SearchText = ""; Pump();
                break;
            }
            case "global":
            {
                // 절반은 실제 문서번호, 절반은 임의 텍스트
                if (rng.Next(2) == 0)
                {
                    var target = nodes[rng.Next(nodes.Count)];
                    vm.GlobalSearch(target.Id); Pump();
                    Assert.Equal("graph", vm.Section);
                    Assert.False(string.IsNullOrEmpty(vm.Graph.SelectedId));
                }
                else
                {
                    vm.GlobalSearch("존재하지않는검색어" + rng.Next(1000)); Pump();
                    Assert.Equal("library", vm.Section);
                }
                break;
            }
            case "graph":
            {
                vm.Section = "graph"; Pump();
                var visible = nodes.Where(n => vm.Graph.IsTypeVisible(n.Type)).ToList();
                var target = visible[rng.Next(visible.Count)];
                vm.Graph.OnNodeSelected(target); Pump();
                Assert.Equal(services.Network.Degree(target.Id), vm.Graph.Neighbors.Count);
                Assert.Contains(target.Id, vm.Graph.SelectedSub);
                if (vm.Graph.Neighbors.Count == 0)
                    friction.Log(persona, task, $"고립 노드(연결 0): {target.Id}");
                break;
            }
            case "graphsearch":
            {
                vm.Section = "graph"; Pump();
                // 기본 레이어에서 숨겨진 양식을 검색 → 레이어 자동 활성 확인
                var form = nodes.First(n => n.Type == NodeTypes.Form);
                vm.Graph.ShowForms = false; Pump();
                vm.Graph.SearchText = form.Id;
                var found = vm.Graph.Search(); Pump();
                Assert.Equal(form.Id, found);
                Assert.True(vm.Graph.ShowForms, "숨김 레이어 자동 활성화 실패");
                break;
            }
            case "drill":
            {
                vm.Section = "drill"; Pump();
                if (kind == "none")
                {
                    Assert.True(vm.Drill.NoRecords, "폴더 미연결인데 CTA 미노출");
                    break;
                }
                var withData = vm.Drill.Procedures.Where(p => p.Count > 0).ToList();
                if (withData.Count == 0)
                {
                    friction.Log(persona, task, $"기록 매칭 0 (변형 {kind})");
                    break;
                }
                var proc = withData[rng.Next(withData.Count)];
                vm.Drill.SelectedProcedure = proc; Pump();
                Assert.NotEmpty(vm.Drill.Groups);
                Assert.NotEmpty(vm.Drill.Records);
                Assert.Contains("›", vm.Drill.Breadcrumb);
                var unmatched = services.Records.Count(r => r.NodeId is null);
                if (services.Records.Count > 0 && unmatched * 100 / services.Records.Count > 30)
                    friction.Log(persona, task, $"미분류 비율 높음 {unmatched}/{services.Records.Count}");
                break;
            }
            case "review":
            {
                if (services.Records.Count == 0) break;
                var record = services.Records[rng.Next(services.Records.Count)];
                vm.ShowTrace(record); Pump();
                Assert.Equal("trace", vm.Section);
                Assert.True(vm.Trace.Lineage.Count >= 1);
                if (rng.Next(4) == 0) // 25%만 실제 점검 실행 (실사용 빈도 반영)
                {
                    vm.Trace.ReviewCommand.Execute(null);
                    PumpUntil(() => !vm.Trace.IsReviewing && vm.Trace.Findings.Count > 0);
                    Assert.Contains("점검 완료", vm.Trace.ReviewStatus);
                    Assert.NotEmpty(vm.Trace.Impacts);
                }
                break;
            }
            case "dashboard":
            {
                vm.Section = "dashboard"; Pump();
                Assert.True(vm.Dashboard.Tiles.Count >= 4);
                Assert.NotEmpty(vm.Dashboard.TypeBars);
                Assert.NotEmpty(vm.Dashboard.CoverageBars);
                break;
            }
            case "export":
            {
                vm.Section = "library"; Pump();
                vm.Library.ExportCsvCommand.Execute(null); Pump();
                Assert.Contains("완료", vm.Library.Summary);
                break;
            }
        }
    }
}
