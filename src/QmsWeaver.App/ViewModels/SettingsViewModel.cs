using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QmsWeaver.Core.Models;

namespace QmsWeaver.App.ViewModels;

public sealed class BindingRow
{
    public FolderBinding Binding { get; init; } = null!;
    public string Display => Binding.NodeId is null
        ? $"[통합 루트] {Binding.Path}"
        : $"[{Binding.NodeId}] {Binding.Path}";
}

public partial class SettingsViewModel : ObservableObject
{
    private readonly AppServices _services;

    public ObservableCollection<BindingRow> Bindings { get; } = new();

    [ObservableProperty] private string _newPath = "";
    [ObservableProperty] private string _newNodeId = "";
    [ObservableProperty] private BindingRow? _selectedBinding;
    [ObservableProperty] private string _scanStatus = "";

    // 화면 표시 설정
    public List<double> FontSizes { get; } = new() { 11, 12, 13, 14, 15, 16, 17 };
    public List<double> RowHeights { get; } = new() { 28, 31, 34, 38, 42, 48 };
    [ObservableProperty] private double _uiFontSize;
    [ObservableProperty] private double _tableRowHeight;

    /// <summary>화면 설정 변경 시 MainWindow가 즉시 적용하도록 통지.</summary>
    public event Action? UiPrefsChanged;

    // AI 설정
    [ObservableProperty] private string _aiBaseUrl;
    [ObservableProperty] private string _aiModel;
    [ObservableProperty] private string _aiApiKey;
    [ObservableProperty] private string _aiStatus = "";
    [ObservableProperty] private bool _isBusy;

    public SettingsViewModel(AppServices services)
    {
        _services = services;
        var ai = services.Config.Config.Ai;
        _aiBaseUrl = ai.BaseUrl;
        _aiModel = ai.Model;
        _aiApiKey = ai.ApiKey;
        _uiFontSize = services.Config.Config.UiFontSize;
        _tableRowHeight = services.Config.Config.TableRowHeight;
        // 스캔 진행률 실시간 반영 — "스캔 중"만 떠 있는 문제 해결
        services.ScanProgressChanged += () => ScanStatus = services.ScanStatus;
        RefreshBindings();
    }

    partial void OnUiFontSizeChanged(double value) => ApplyUiPrefs();
    partial void OnTableRowHeightChanged(double value) => ApplyUiPrefs();

    private void ApplyUiPrefs()
    {
        _services.Config.Config.UiFontSize = UiFontSize;
        _services.Config.Config.TableRowHeight = TableRowHeight;
        _services.Config.Save();
        UiPrefsChanged?.Invoke();
    }

    /// <summary>네이티브 폴더 선택 다이얼로그 결과를 입력칸에 채운다 (뷰에서 호출).</summary>
    public void SetPickedFolder(string path) => NewPath = path;

    private void RefreshBindings()
    {
        Bindings.Clear();
        foreach (var b in _services.Config.Config.FolderBindings)
            Bindings.Add(new BindingRow { Binding = b });
    }

    [RelayCommand]
    private void AddBinding()
    {
        var path = NewPath.Trim().Trim('"');
        if (path.Length == 0) { ScanStatus = "폴더 경로를 입력하세요"; return; }
        if (!Directory.Exists(path)) { ScanStatus = $"폴더가 존재하지 않습니다: {path}"; return; }

        var nodeId = NewNodeId.Trim();
        if (nodeId.Length > 0 && !_services.Network.NodesById.ContainsKey(nodeId))
        {
            ScanStatus = $"문서번호 '{nodeId}'가 등록 목록에 없습니다 (비우면 통합 루트)";
            return;
        }

        _services.Config.Config.FolderBindings.Add(new FolderBinding
        {
            Path = path,
            NodeId = nodeId.Length == 0 ? null : nodeId,
        });
        _services.Config.Save();
        _services.Audit.Log("config.binding.add", path, nodeId);
        NewPath = "";
        NewNodeId = "";
        ScanStatus = "폴더가 연결되었습니다 — [지금 스캔]을 눌러 기록을 수집하세요";
        RefreshBindings();
    }

    [RelayCommand]
    private void RemoveBinding()
    {
        if (SelectedBinding is null) return;
        _services.Config.Config.FolderBindings.Remove(SelectedBinding.Binding);
        _services.Config.Save();
        _services.Audit.Log("config.binding.remove", SelectedBinding.Binding.Path);
        RefreshBindings();
    }

    [RelayCommand]
    private async Task ScanNowAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ScanStatus = "스캔 시작…";
        try
        {
            await _services.RescanAsync();
            var unmatched = _services.Records.Count(r => r.NodeId is null);
            var old = _services.Records.Count(r => !r.IsCurrent);
            ScanStatus = $"스캔 완료 — 기록 {_services.Records.Count}건" +
                         (old > 0 ? $" (현행 {_services.Records.Count - old} · 구버전 {old})" : "") +
                         (unmatched > 0 ? $" · 미분류 {unmatched}건 → 기록 탐색의 인박스 확인" : "");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SaveAi()
    {
        var ai = _services.Config.Config.Ai;
        ai.BaseUrl = AiBaseUrl.Trim();
        ai.Model = AiModel.Trim();
        ai.ApiKey = AiApiKey.Trim();
        _services.Config.Save();
        _services.Audit.Log("config.ai.save", detail: $"model={ai.Model}");
        AiStatus = ai.Enabled ? "저장됨 — AI 분석 활성" : "저장됨 — 키가 비어 있어 로컬 분석만 동작";
    }

    [RelayCommand]
    private async Task TestAiAsync()
    {
        if (IsBusy) return;
        SaveAi();
        if (!_services.Ai.IsConfigured) { AiStatus = "API 키를 먼저 입력하세요"; return; }
        IsBusy = true;
        AiStatus = "연결 테스트 중…";
        try
        {
            var (ok, message) = await _services.Ai.TestConnectionAsync();
            AiStatus = message;
            _services.Audit.Log("config.ai.test", detail: ok ? "ok" : "fail");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
