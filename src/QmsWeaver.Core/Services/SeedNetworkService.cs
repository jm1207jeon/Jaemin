using System.Reflection;
using System.Text.Json;
using QmsWeaver.Core.Models;

namespace QmsWeaver.Core.Services;

/// <summary>임베디드 seedNetwork.json을 로드하고 인덱스를 제공한다.</summary>
public sealed class SeedNetworkService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public DocNetwork Network { get; }
    public IReadOnlyDictionary<string, DocNode> NodesById { get; }
    private readonly Dictionary<string, List<(DocNode Other, DocEdge Edge)>> _adjacency = new();

    public SeedNetworkService() : this(LoadEmbedded()) { }

    public SeedNetworkService(DocNetwork network)
    {
        Network = network;
        NodesById = network.Nodes.ToDictionary(n => n.Id);
        foreach (var e in network.Edges)
        {
            if (!NodesById.TryGetValue(e.Source, out var s) || !NodesById.TryGetValue(e.Target, out var t))
                continue;
            Add(e.Source, (t, e));
            Add(e.Target, (s, e));
        }
    }

    private void Add(string id, (DocNode, DocEdge) entry)
    {
        if (!_adjacency.TryGetValue(id, out var list))
            _adjacency[id] = list = new();
        list.Add(entry);
    }

    public IReadOnlyList<(DocNode Other, DocEdge Edge)> Neighbors(string id) =>
        _adjacency.TryGetValue(id, out var list) ? list : Array.Empty<(DocNode, DocEdge)>();

    public int Degree(string id) => Neighbors(id).Count;

    private static DocNetwork LoadEmbedded()
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames()
            .First(n => n.EndsWith("seedNetwork.json", StringComparison.OrdinalIgnoreCase));
        using var stream = asm.GetManifestResourceStream(name)!;
        return JsonSerializer.Deserialize<DocNetwork>(stream, JsonOpts)
               ?? throw new InvalidOperationException("seedNetwork.json 파싱 실패");
    }
}
