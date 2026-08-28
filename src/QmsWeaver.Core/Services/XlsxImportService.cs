using System.Text.RegularExpressions;
using ClosedXML.Excel;
using QmsWeaver.Core.Models;

namespace QmsWeaver.Core.Services;

/// <summary>
/// 문서 등록 이력 대장(F401-09) xlsx에서 문서 목록을 갱신 임포트한다.
/// tools/build_network.py와 동일한 시트·컬럼 규칙.
/// </summary>
public static partial class XlsxImportService
{
    [GeneratedRegex(@"^(QM|QP|SOP|F)[\d-]+$")] private static partial Regex DocIdRx();

    private static readonly (string Sheet, string Type)[] Sheets =
    {
        ("매뉴얼 절차서_Approved(편집X)", NodeTypes.Procedure),
        ("지침서_Approved(편집X)", NodeTypes.Sop),
        ("작업표준서_Approved(편집X)", NodeTypes.WorkStandard),
        ("양식_Approved(편집X)", NodeTypes.Form),
    };

    public sealed record ImportResult(List<DocNode> Nodes, List<string> DuplicateIds);

    public static ImportResult Import(string xlsxPath)
    {
        using var wb = new XLWorkbook(xlsxPath);
        var nodes = new List<DocNode>();
        var seen = new HashSet<string>();
        var dups = new List<string>();

        foreach (var (sheetName, type) in Sheets)
        {
            if (!wb.TryGetWorksheet(sheetName, out var ws)) continue;
            foreach (var row in ws.RowsUsed().Skip(1))
            {
                var id = row.Cell(1).GetString().Trim();
                if (!DocIdRx().IsMatch(id)) continue;

                if (!seen.Add(id)) { dups.Add(id); continue; }

                var rawName = row.Cell(2).GetString();
                var parts = rawName.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var node = new DocNode
                {
                    Id = id,
                    Name = parts.Length > 0 ? parts[0] : id,
                    NameEn = parts.Length > 1 ? parts[1] : null,
                    Type = id.StartsWith("QM-", StringComparison.Ordinal) ? NodeTypes.Manual : type,
                    Rev = row.Cell(3).GetString().Trim(),
                    Dept = row.Cell(6).GetString().Trim(),
                };
                var eff = row.Cell(5);
                node.Effective = eff.DataType == XLDataType.DateTime
                    ? eff.GetDateTime().ToString("yyyy-MM-dd")
                    : eff.GetString().Trim();
                if (Regex.Match(id, @"^(?:QP-|SOP-|F)(\d)") is { Success: true } m)
                    node.Clause = int.Parse(m.Groups[1].Value);
                nodes.Add(node);
            }
        }
        return new ImportResult(nodes, dups);
    }
}
