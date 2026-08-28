using QmsWeaver.Core.Models;
using QmsWeaver.Core.Services;
using Xunit;

namespace QmsWeaver.Core.Tests;

public class HierarchyTests
{
    [Theory]
    [InlineData("QP-706", "QM-001")]
    [InlineData("SOP-805-05", "QP-805")]
    [InlineData("F805-05-01", "SOP-805-05")]
    [InlineData("F401-09", "QP-401")]
    [InlineData("QM-001", null)]
    public void ParentOf_FollowsNumberingScheme(string id, string? expected) =>
        Assert.Equal(expected, HierarchyService.ParentOf(id));

    [Fact]
    public void ExtractCitations_FindsDocIdsWithRev()
    {
        var text = "본 보고서는 SOP-705-03 Rev.6 및 QP-706에 따라 수행되었다. 양식 F706-02 사용.";
        var citations = HierarchyService.ExtractCitations(text).ToList();
        Assert.Contains(citations, c => c.DocId == "SOP-705-03" && c.CitedRev == 6);
        Assert.Contains(citations, c => c.DocId == "QP-706");
        Assert.Contains(citations, c => c.DocId == "F706-02");
    }

    [Fact]
    public void PromoteToProcedure_LiftsFormToQp() =>
        Assert.Equal("QP-805", FolderScanService.PromoteToProcedure("F805-05-01"));
}

public class SeedNetworkTests
{
    [Fact]
    public void EmbeddedSeed_LoadsWithExpectedShape()
    {
        var net = new SeedNetworkService();
        Assert.True(net.Network.Nodes.Count > 300);
        Assert.True(net.Network.Edges.Count > 400);
        Assert.True(net.NodesById.ContainsKey("QM-001"));
        Assert.True(net.NodesById.ContainsKey("QP-706"));
        Assert.True(net.Degree("QM-001") > 10);
    }

    [Fact]
    public void Lineage_RunsFromExternalToDocument()
    {
        var net = new SeedNetworkService();
        var chain = new HashSet<string>(HierarchyService.Lineage(net, "F706-02").Select(n => n.Id));
        Assert.Contains("QP-706", chain);
        Assert.Contains("QM-001", chain);
        Assert.Contains("F706-02", chain);
    }
}

public class RulesEngineTests
{
    private static readonly SeedNetworkService Net = new();

    [Fact]
    public void Review_FlagsRevMismatch()
    {
        var engine = new RulesEngine(Net);
        var current = Net.NodesById["QP-706"].Rev; // 현행 Rev
        Assert.False(string.IsNullOrEmpty(current));
        var staleRev = int.Parse(current!) - 1;
        var record = new RecordEntry
        {
            FilePath = "x", Title = "PQ", Author = "a", Reviewer = "b", Approver = "c",
            Rev = 1, PerformedDate = new DateOnly(2025, 5, 20),
        };
        var findings = engine.Review(record, $"QP-706 Rev.{staleRev}에 따라 수행", Array.Empty<RecordEntry>());
        Assert.Contains(findings, f => f.Severity == ReviewSeverity.Fail && f.Title.Contains("개정번호"));
    }

    [Fact]
    public void ImpactOfRevision_IncludesDocControlBasics()
    {
        var engine = new RulesEngine(Net);
        var items = engine.ImpactOfRevision("SOP-705-03");
        Assert.Contains(items, i => i.Basis.Contains("F401-08"));
        Assert.Contains(items, i => i.Basis.Contains("F401-09"));
        Assert.Contains(items, i => i.Action.Contains("적격성"));
        Assert.Contains(items, i => i.Action.Contains("재밸리데이션"));
    }
}

public class RecordStatusTests
{
    [Fact]
    public void Status_DerivedFromSignatures()
    {
        Assert.Equal(RecordStatus.Approved,
            new RecordEntry { Author = "a", Reviewer = "b", Approver = "c" }.Status);
        Assert.Equal(RecordStatus.MissingApproval,
            new RecordEntry { Author = "a", Reviewer = "b" }.Status);
        Assert.Equal(RecordStatus.Draft, new RecordEntry().Status);
    }
}

public class AiParseTests
{
    [Fact]
    public void ParseFindings_HandlesCodeFence()
    {
        var text = "```json\n[{\"severity\":\"fail\",\"title\":\"수치 불일치\",\"detail\":\"온도 범위 상이\"}]\n```";
        var findings = AiService.ParseFindings(text);
        var f = Assert.Single(findings);
        Assert.Equal(ReviewSeverity.Fail, f.Severity);
        Assert.True(f.FromAi);
    }
}

public class FolderScanTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "qms-test-" + Guid.NewGuid().ToString("N"));

    public FolderScanTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "열처리", "2025"));
        File.WriteAllText(Path.Combine(_root, "열처리", "2025", "PQ보고서_QP-706_Rev.1_2025-05-20.txt"), "test");
        File.WriteAllText(Path.Combine(_root, "열처리", "2025", "일탈보고서 F706-01.txt"), "test");
    }

    [Fact]
    public void Scan_ExtractsIdRevDateAndGroups()
    {
        var svc = new FolderScanService(new SeedNetworkService());
        var records = svc.Scan(new[] { new FolderBinding { Path = _root } });
        Assert.Equal(2, records.Count);

        var pq = records.Single(r => r.FileName.StartsWith("PQ"));
        Assert.Equal("QP-706", pq.NodeId);
        Assert.Equal(1, pq.Rev);
        Assert.Equal(new DateOnly(2025, 5, 20), pq.PerformedDate);
        Assert.Equal("열처리", pq.Group);
        Assert.Equal("2025", pq.Unit);

        var dev = records.Single(r => r.FileName.StartsWith("일탈"));
        Assert.Equal("QP-706", dev.NodeId); // F706-01 → QP-706 승격
    }

    public void Dispose() => Directory.Delete(_root, true);
}
