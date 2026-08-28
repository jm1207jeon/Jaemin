using System.Text.Json.Serialization;

namespace QmsAnalyzer.Core.Models;

/// <summary>문서/외부요구/기술문서 노드. seedNetwork.json 스키마와 1:1.</summary>
public sealed class DocNode
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("nameEn")] public string? NameEn { get; set; }
    [JsonPropertyName("type")] public string Type { get; set; } = "form";
    [JsonPropertyName("rev")] public string? Rev { get; set; }
    [JsonPropertyName("effective")] public string? Effective { get; set; }
    [JsonPropertyName("dept")] public string? Dept { get; set; }
    [JsonPropertyName("clause")] public int? Clause { get; set; }
    [JsonPropertyName("desc")] public string? Desc { get; set; }
    [JsonPropertyName("gap")] public bool? Gap { get; set; }
    [JsonPropertyName("src")] public string? Src { get; set; }
}

public sealed class DocEdge
{
    [JsonPropertyName("source")] public string Source { get; set; } = "";
    [JsonPropertyName("target")] public string Target { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "hierarchy";
    [JsonPropertyName("label")] public string? Label { get; set; }
    [JsonPropertyName("strength")] public double Strength { get; set; } = 0.7;
}

public sealed class NetworkMeta
{
    [JsonPropertyName("source")] public string? Source { get; set; }
    [JsonPropertyName("nodeCounts")] public Dictionary<string, int> NodeCounts { get; set; } = new();
    [JsonPropertyName("edgeCount")] public int EdgeCount { get; set; }
}

public sealed class DocNetwork
{
    [JsonPropertyName("meta")] public NetworkMeta Meta { get; set; } = new();
    [JsonPropertyName("nodes")] public List<DocNode> Nodes { get; set; } = new();
    [JsonPropertyName("edges")] public List<DocEdge> Edges { get; set; } = new();
}

public static class NodeTypes
{
    public const string Manual = "manual";
    public const string Procedure = "procedure";
    public const string Sop = "sop";
    public const string WorkStandard = "work-standard";
    public const string Form = "form";
    public const string Standard = "standard";
    public const string Regulation = "regulation";
    public const string TechDoc = "techdoc";

    /// <summary>UI 표시용 한국어 라벨.</summary>
    public static string Label(string type) => type switch
    {
        Manual => "품질매뉴얼",
        Procedure => "절차서",
        Sop => "지침서",
        WorkStandard => "작업표준서",
        Form => "양식",
        Standard => "규격",
        Regulation => "규제",
        TechDoc => "기술문서",
        _ => type,
    };
}

public static class EdgeTypes
{
    public const string Hierarchy = "hierarchy";
    public const string External = "external";
    public const string Process = "process";
    public const string Derived = "derived";

    public static string Label(string type) => type switch
    {
        Hierarchy => "계층",
        External => "규제요구",
        Process => "프로세스",
        Derived => "파생",
        _ => type,
    };
}
