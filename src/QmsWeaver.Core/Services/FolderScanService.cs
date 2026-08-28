using System.Text.RegularExpressions;
using QmsWeaver.Core.Models;

namespace QmsWeaver.Core.Services;

/// <summary>
/// 지정 폴더를 재귀 스캔하여 기록(RecordEntry)을 수집한다.
/// 우선순위: 노드별 지정 폴더 > 통합 루트 + 자동 매칭 규칙.
/// 파일명·폴더명에서 문서번호/일자/Rev/서명자를 패턴으로 추출한다.
/// </summary>
public sealed partial class FolderScanService
{
    [GeneratedRegex(@"(20\d{2})[-._]?(\d{2})[-._]?(\d{2})")] private static partial Regex DateRx();
    [GeneratedRegex(@"[Rr]ev\.?\s*_?(\d{1,3})")] private static partial Regex RevRx();
    [GeneratedRegex(@"\b(20\d{2})\b")] private static partial Regex YearRx();

    private static readonly string[] RecordExtensions =
        { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".hwp", ".hwpx", ".txt" };

    private readonly SeedNetworkService _net;
    // 노드 이름 → ID 매핑 (파일명에 문서번호가 없을 때 이름으로 2차 매칭)
    private readonly List<(string Keyword, string NodeId)> _nameIndex;

    public FolderScanService(SeedNetworkService net)
    {
        _net = net;
        // 양식 이름 포함 (시뮬레이션 발견: "회의록", "출하검사성적서" 등 양식명 파일이 미분류되던 문제).
        // 짧은 일반명사 오탐을 줄이기 위해 3글자 이상 + 가장 긴 키워드 우선 매칭.
        _nameIndex = net.Network.Nodes
            .Where(n => n.Type is NodeTypes.Procedure or NodeTypes.Sop
                        or NodeTypes.WorkStandard or NodeTypes.Form)
            .Where(n => n.Name.Length >= 3)
            .Select(n => (n.Name.Replace(" ", ""), n.Id))
            .ToList();
    }

    public sealed record ScanProgress(int FilesSeen, int RecordsFound, string CurrentDir);

    /// <param name="progress">진행 상황 보고 (파일 수·현재 폴더) — UI가 "멈춘 것처럼" 보이지 않게 한다.</param>
    /// <param name="onBatch">부분 결과 콜백 — 스캔되는 대로 목록에 반영할 수 있다 (배치 단위).</param>
    public IReadOnlyList<RecordEntry> Scan(
        IEnumerable<FolderBinding> bindings,
        IProgress<ScanProgress>? progress = null,
        Action<IReadOnlyList<RecordEntry>>? onBatch = null,
        CancellationToken ct = default)
    {
        var results = new List<RecordEntry>();
        var batch = new List<RecordEntry>();
        var filesSeen = 0;
        var lastDir = "";

        foreach (var b in bindings)
        {
            if (!Directory.Exists(b.Path)) continue;
            var opt = b.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            foreach (var file in Directory.EnumerateFiles(b.Path, "*.*", opt))
            {
                ct.ThrowIfCancellationRequested();
                filesSeen++;
                var dir = Path.GetDirectoryName(file) ?? "";
                if (progress is not null && (filesSeen % 40 == 0 || dir != lastDir))
                {
                    lastDir = dir;
                    progress.Report(new ScanProgress(filesSeen, results.Count, dir));
                }

                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (!RecordExtensions.Contains(ext)) continue;
                if (Path.GetFileName(file).StartsWith("~$")) continue; // Office 임시파일
                var entry = BuildEntry(file, b);
                results.Add(entry);
                batch.Add(entry);
                if (onBatch is not null && batch.Count >= 60)
                {
                    onBatch(batch.ToArray());
                    batch.Clear();
                }
            }
        }
        if (onBatch is not null && batch.Count > 0) onBatch(batch.ToArray());

        PostProcess(results);
        progress?.Report(new ScanProgress(filesSeen, results.Count, ""));
        return results;
    }

    /// <summary>
    /// 후처리 1) 같은 기록의 docx·pdf 병존 시 docx를 대표로 통합 (요구: docx 우선, 없으면 pdf)
    ///        2) 같은 문서의 여러 개정본 중 최신본만 IsCurrent — 구버전도 목록에 남는다.
    /// </summary>
    internal static void PostProcess(List<RecordEntry> records)
    {
        foreach (var r in records)
            r.NormalizedTitle = NormalizeTitle(r.FileName);

        // 1) 형식 통합: 동일 위치·동일 정규화제목·동일 Rev → 대표 형식 선택
        var formatRank = new Dictionary<string, int>
            { ["DOCX"] = 0, ["DOC"] = 1, ["XLSX"] = 2, ["XLS"] = 3, ["PDF"] = 4, ["HWP"] = 5, ["HWPX"] = 6, ["TXT"] = 7 };
        int Rank(RecordEntry r) => formatRank.GetValueOrDefault(r.Format, 9);

        var toRemove = new HashSet<RecordEntry>();
        foreach (var grp in records.GroupBy(r =>
                     (Dir: Path.GetDirectoryName(r.FilePath), r.NodeId, r.NormalizedTitle, r.Rev)))
        {
            var members = grp.OrderBy(Rank).ToList();
            if (members.Count <= 1) continue;
            var primary = members[0];
            foreach (var dup in members.Skip(1))
            {
                if (!primary.AltFormats.Contains(dup.Format))
                    primary.AltFormats.Add(dup.Format);
                toRemove.Add(dup);
            }
        }
        records.RemoveAll(toRemove.Contains);

        // 2) 개정본: 동일 문서(노드+그룹+실행단위+정규화제목)에서 Rev(없으면 수행일) 최신본만 현행.
        //    실행 단위(연차)가 다르면 별개 수행 기록이므로 개정본으로 묶지 않는다.
        foreach (var grp in records.GroupBy(r => (r.NodeId, r.Group, r.Unit, r.NormalizedTitle)))
        {
            var members = grp.ToList();
            if (members.Count <= 1) continue;
            var current = members
                .OrderByDescending(r => r.Rev ?? int.MinValue)
                .ThenByDescending(r => r.PerformedDate ?? DateOnly.MinValue)
                .ThenByDescending(r => r.ModifiedUtc)
                .First();
            foreach (var m in members)
                m.IsCurrent = ReferenceEquals(m, current);
        }
    }

    /// <summary>Rev·일자·확장자·괄호숫자를 제거한 제목 — 개정본 그룹핑 키.</summary>
    internal static string NormalizeTitle(string fileName)
    {
        var s = Path.GetFileNameWithoutExtension(fileName);
        s = RevRx().Replace(s, "");
        s = DateRx().Replace(s, "");
        s = Regex.Replace(s, @"\((\d+)\)|복사본", "");
        s = Regex.Replace(s, @"[_\-\s]+", "");
        return s.ToLowerInvariant();
    }

    private RecordEntry BuildEntry(string file, FolderBinding binding)
    {
        var name = Path.GetFileNameWithoutExtension(file);
        var relDirs = RelativeDirs(file, binding.Path);
        var info = new FileInfo(file);

        var entry = new RecordEntry
        {
            FilePath = file,
            FileName = Path.GetFileName(file),
            Title = name.Replace('_', ' ').Trim(),
            SizeBytes = info.Length,
            ModifiedUtc = info.LastWriteTimeUtc,
        };

        // 1) 문서번호 직접 매칭 (파일명 → 상위 폴더 순)
        entry.NodeId = MatchDocId(name) ?? relDirs.Select(MatchDocId).FirstOrDefault(x => x is not null);
        // 2) 노드별 바인딩이면 그 노드로 귀속
        entry.NodeId ??= binding.NodeId;
        // 3) 이름 키워드 매칭
        entry.NodeId ??= MatchByName(name) ?? relDirs.Select(MatchByName).FirstOrDefault(x => x is not null);

        // 기록은 양식/지침 매칭 시 담당 절차서 수준으로 승격해 드릴다운 1열과 맞춘다.
        if (entry.NodeId is not null) entry.NodeId = PromoteToProcedure(entry.NodeId);

        // 그룹/실행 단위: 바인딩 루트 아래 폴더 구조에서 유도
        entry.Group = relDirs.FirstOrDefault(d => !LooksLikeUnit(d));
        entry.Unit = relDirs.LastOrDefault(LooksLikeUnit)
                     ?? (YearRx().Match(name) is { Success: true } y ? y.Groups[1].Value : null);

        var d = DateRx().Match(name);
        if (d.Success && DateOnly.TryParse($"{d.Groups[1].Value}-{d.Groups[2].Value}-{d.Groups[3].Value}", out var date))
            entry.PerformedDate = date;
        entry.PerformedDate ??= DateOnly.FromDateTime(info.LastWriteTime);

        var r = RevRx().Match(name);
        if (r.Success) entry.Rev = int.Parse(r.Groups[1].Value);

        return entry;
    }

    private static List<string> RelativeDirs(string file, string root)
    {
        var rel = Path.GetRelativePath(root, Path.GetDirectoryName(file)!);
        return rel == "." ? new() : rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToList();
    }

    private string? MatchDocId(string text)
    {
        var m = HierarchyService.DocIdRx().Match(text);
        return m.Success && _net.NodesById.ContainsKey(m.Value) ? m.Value : null;
    }

    private string? MatchByName(string text)
    {
        var compact = text.Replace(" ", "");
        string? best = null;
        var bestLen = 0;
        foreach (var (keyword, nodeId) in _nameIndex)
        {
            if (keyword.Length > bestLen && compact.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                best = nodeId;
                bestLen = keyword.Length;
            }
        }
        return best;
    }

    /// <summary>양식/지침 ID를 담당 절차서(QP-xxx)로 승격한다.</summary>
    public static string PromoteToProcedure(string nodeId)
    {
        var cur = nodeId;
        while (cur is not null && !cur.StartsWith("QP-", StringComparison.Ordinal) && cur != "QM-001")
            cur = HierarchyService.ParentOf(cur) ?? "";
        return string.IsNullOrEmpty(cur) ? nodeId : cur;
    }

    private static bool LooksLikeUnit(string dir) =>
        YearRx().IsMatch(dir) || dir.Contains("차수") || dir.Contains("정기") || dir.Contains("재V", StringComparison.OrdinalIgnoreCase);
}
