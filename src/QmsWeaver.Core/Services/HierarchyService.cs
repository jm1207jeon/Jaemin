using System.Text.RegularExpressions;
using QmsWeaver.Core.Models;

namespace QmsWeaver.Core.Services;

/// <summary>
/// 문서번호 체계가 인코딩하는 계층을 해석한다.
///   F805-05-01 → SOP-805-05 → QP-805 → QM-001
///   절차서 번호 첫 자리 = ISO 13485 조항(4~8)
/// </summary>
public static partial class HierarchyService
{
    [GeneratedRegex(@"^SOP-(\d{3})-\d+$")] private static partial Regex SopRx();
    [GeneratedRegex(@"^F(\d{3})-(\d+)-(\d+)$")] private static partial Regex FormUnderSopRx();
    [GeneratedRegex(@"^F(\d{3})-(\d+)$")] private static partial Regex FormRx();
    // \b는 한글/밑줄 앞뒤에서 경계로 동작하지 않으므로 명시적 lookaround 사용
    // (예: "QP-706에", "QP-706_Rev.1" 모두 매칭되어야 함)
    [GeneratedRegex(@"(?<![A-Za-z0-9-])(QM-\d{3}|QP-\d{3}|SOP-\d{3}-\d{1,3}|F\d{3}-\d{1,3}(?:-\d{1,3})?)(?![\d-])")]
    public static partial Regex DocIdRx();

    public static string? ParentOf(string docId)
    {
        if (docId == "QM-001") return null;
        if (docId.StartsWith("QP-", StringComparison.Ordinal)) return "QM-001";
        var m = SopRx().Match(docId);
        if (m.Success) return $"QP-{m.Groups[1].Value}";
        m = FormUnderSopRx().Match(docId);
        if (m.Success) return $"SOP-{m.Groups[1].Value}-{m.Groups[2].Value}";
        m = FormRx().Match(docId);
        if (m.Success) return $"QP-{m.Groups[1].Value}";
        return null;
    }

    /// <summary>기록/문서에서 규격·매뉴얼까지의 계보 사슬 (자신 → ... → QM-001 → 규격/규제).</summary>
    public static List<DocNode> Lineage(SeedNetworkService net, string docId)
    {
        var chain = new List<DocNode>();
        var cur = docId;
        while (cur is not null && net.NodesById.TryGetValue(cur, out var node))
        {
            chain.Add(node);
            cur = ParentOf(cur);
        }
        // 사슬 상 노드들에 붙은 규제·규격을 최상위로 덧붙인다.
        var externals = chain
            .SelectMany(n => net.Neighbors(n.Id))
            .Where(x => x.Edge.Type == EdgeTypes.External)
            .Select(x => x.Other)
            .DistinctBy(n => n.Id)
            .ToList();
        chain.Reverse(); // 규격/매뉴얼이 왼쪽에 오도록: [QM-001, QP, SOP, F]
        return externals.Concat(chain).ToList();
    }

    /// <summary>텍스트에서 문서번호 인용을 추출한다 (예: 보고서 본문 내 "SOP-705-03 Rev.6").</summary>
    public static IEnumerable<(string DocId, int? CitedRev)> ExtractCitations(string text)
    {
        foreach (Match m in DocIdRx().Matches(text))
        {
            int? rev = null;
            var tail = text[m.Index..Math.Min(text.Length, m.Index + m.Length + 24)];
            var revMatch = Regex.Match(tail, @"Rev\.?\s*(\d{1,3})", RegexOptions.IgnoreCase);
            if (revMatch.Success) rev = int.Parse(revMatch.Groups[1].Value);
            yield return (m.Value, rev);
        }
    }
}
