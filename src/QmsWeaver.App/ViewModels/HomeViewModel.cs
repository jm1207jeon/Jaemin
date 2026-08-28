using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using QmsWeaver.Core.Models;

namespace QmsWeaver.App.ViewModels;

public sealed class TaskTile
{
    public string Icon { get; init; } = "";
    public string Title { get; init; } = "";
    public string Desc { get; init; } = "";
    public string Roles { get; init; } = "";
    public string Section { get; init; } = "";
}

/// <summary>
/// 시작 화면 — 과업 중심(task-first) 타일.
/// "오늘 무엇을 하시겠습니까?"에서 한 번의 클릭으로 목적 화면에 도달한다.
/// </summary>
public partial class HomeViewModel : ObservableObject
{
    private readonly AppServices _services;

    public List<TaskTile> Tiles { get; } = new()
    {
        new() { Icon = "🔎", Title = "문서 찾기·열람", Desc = "문서번호·이름으로 검색하고 Rev/적용일 확인", Roles = "전 부서", Section = "library" },
        new() { Icon = "🕸", Title = "문서 관계 지도", Desc = "우리 QMS의 전체 구조를 한눈에 — 신규 입사자 교육에도", Roles = "QMR · 교육", Section = "graph" },
        new() { Icon = "🗂", Title = "기록 탐색", Desc = "절차 → 종류 → 차수 → 수행 기록으로 단계별 드릴다운", Roles = "QA · QC", Section = "drill" },
        new() { Icon = "✅", Title = "기록 검토·정합성 점검", Desc = "인용 Rev·서명·주기 자동 점검 + AI 문안 대조", Roles = "QA · QMR", Section = "trace" },
        new() { Icon = "📋", Title = "심사 대비 현황", Desc = "규제 커버리지·갭·무결성 경고·감사 추적", Roles = "품질팀장 · RA", Section = "dashboard" },
        new() { Icon = "⚙", Title = "폴더 연결·AI 설정", Desc = "기록 폴더 연결, 스캔, Claude API 연결", Roles = "관리자", Section = "settings" },
    };

    [ObservableProperty] private string _statusLine = "";
    public ObservableCollection<string> Recent { get; } = new();

    public HomeViewModel(AppServices services)
    {
        _services = services;
        services.RecordsChanged += Refresh;
        Refresh();
    }

    public void Refresh()
    {
        var docs = _services.Network.Network.Nodes
            .Count(n => n.Type is not (NodeTypes.Standard or NodeTypes.Regulation or NodeTypes.TechDoc));
        var missing = _services.Records.Count(r => r.Status == RecordStatus.MissingApproval);
        var folders = _services.Config.Config.FolderBindings.Count;
        StatusLine = $"문서 {docs}건 · 기록 {_services.Records.Count}건 · 연결 폴더 {folders}개"
                     + (missing > 0 ? $" · ⚠ 승인 서명 누락 {missing}건" : "")
                     + (_services.Ai.IsConfigured ? " · AI 연결됨" : " · AI 미연결");

        Recent.Clear();
        foreach (var line in _services.Audit.Tail(6).Reverse())
            Recent.Add(line);
    }
}
