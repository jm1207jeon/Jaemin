using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using QmsAnalyzer.Core.Models;
using QmsAnalyzer.Core.Services;

namespace QmsAnalyzer.App.Controls;

/// <summary>
/// Force-directed 문서 네트워크 그래프.
/// 드래그(노드/배경), 휠 줌, 클릭 선택 + 이웃 강조를 지원한다.
/// </summary>
public sealed class NetworkGraphControl : Control
{
    private sealed class VNode
    {
        public required DocNode Doc;
        public double X, Y, Vx, Vy;
        public double R;
        public bool Pinned;
    }

    private sealed class VEdge
    {
        public required VNode A;
        public required VNode B;
        public required DocEdge Doc;
    }

    private SeedNetworkService? _net;
    private Func<string, bool> _typeVisible = _ => true;
    private Action<DocNode?>? _onSelect;

    private readonly List<VNode> _nodes = new();
    private readonly List<VEdge> _edges = new();
    private readonly Dictionary<string, VNode> _byId = new();
    private readonly Random _rng = new(7);

    private VNode? _selected;
    private HashSet<string> _neighborIds = new();

    private double _zoom = 1.0;
    private Point _pan = new(0, 0);
    private VNode? _dragNode;
    private Point? _dragBackgroundFrom;
    private Point _lastPointer;

    private readonly DispatcherTimer _timer;
    private int _ticksLeft;

    public NetworkGraphControl()
    {
        ClipToBounds = true;
        Focusable = true;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _timer.Tick += (_, _) => Tick();
    }

    public void Initialize(SeedNetworkService net, Func<string, bool> typeVisible, Action<DocNode?> onSelect)
    {
        _net = net;
        _typeVisible = typeVisible;
        _onSelect = onSelect;
        Rebuild();
    }

    public void RefreshFilter() => Rebuild(keepPositions: true);

    public void SelectById(string nodeId)
    {
        if (_byId.TryGetValue(nodeId, out var node)) Select(node);
    }

    private void Rebuild(bool keepPositions = false)
    {
        if (_net is null) return;
        var oldPos = keepPositions
            ? _nodes.ToDictionary(n => n.Doc.Id, n => (n.X, n.Y))
            : new Dictionary<string, (double, double)>();

        _nodes.Clear();
        _edges.Clear();
        _byId.Clear();

        var w = Math.Max(Bounds.Width, 600);
        var h = Math.Max(Bounds.Height, 400);

        foreach (var doc in _net.Network.Nodes.Where(n => _typeVisible(n.Type)))
        {
            var degree = _net.Degree(doc.Id);
            var baseR = doc.Type switch
            {
                NodeTypes.Manual => 16,
                NodeTypes.Regulation => 11,
                NodeTypes.Standard => 9,
                NodeTypes.TechDoc => 8,
                NodeTypes.Procedure => 7,
                NodeTypes.Form => 3.5,
                _ => 5,
            };
            var node = new VNode
            {
                Doc = doc,
                R = baseR + Math.Min(8, Math.Sqrt(degree)),
            };
            if (oldPos.TryGetValue(doc.Id, out var p)) (node.X, node.Y) = p;
            else
            {
                node.X = w / 2 + (_rng.NextDouble() - 0.5) * w * 0.8;
                node.Y = h / 2 + (_rng.NextDouble() - 0.5) * h * 0.8;
            }
            _nodes.Add(node);
            _byId[doc.Id] = node;
        }

        foreach (var e in _net.Network.Edges)
        {
            if (_byId.TryGetValue(e.Source, out var a) && _byId.TryGetValue(e.Target, out var b))
                _edges.Add(new VEdge { A = a, B = b, Doc = e });
        }

        if (_selected is not null && !_byId.ContainsKey(_selected.Doc.Id)) Select(null);
        else if (_selected is not null) _selected = _byId[_selected.Doc.Id];

        _ticksLeft = 260;
        _timer.Start();
        InvalidateVisual();
    }

    // ── 물리 시뮬레이션 ───────────────────────────────────────────────
    private void Tick()
    {
        if (_ticksLeft-- <= 0) { _timer.Stop(); return; }
        var alpha = Math.Max(0.02, _ticksLeft / 260.0);

        // 반발력
        for (var i = 0; i < _nodes.Count; i++)
        {
            var a = _nodes[i];
            for (var j = i + 1; j < _nodes.Count; j++)
            {
                var b = _nodes[j];
                var dx = a.X - b.X;
                var dy = a.Y - b.Y;
                var d2 = dx * dx + dy * dy + 0.01;
                if (d2 > 90000) continue;
                var force = (a.Doc.Type == NodeTypes.Form || b.Doc.Type == NodeTypes.Form ? 500 : 2600) / d2 * alpha;
                var d = Math.Sqrt(d2);
                var fx = dx / d * force;
                var fy = dy / d * force;
                a.Vx += fx; a.Vy += fy;
                b.Vx -= fx; b.Vy -= fy;
            }
        }

        // 스프링(엣지)
        foreach (var e in _edges)
        {
            var target = e.Doc.Type switch
            {
                EdgeTypes.Hierarchy => 55.0,
                EdgeTypes.External => 120.0,
                _ => 95.0,
            };
            var dx = e.B.X - e.A.X;
            var dy = e.B.Y - e.A.Y;
            var d = Math.Sqrt(dx * dx + dy * dy) + 0.01;
            var k = (e.Doc.Type == EdgeTypes.Hierarchy ? 0.06 : 0.02) * alpha;
            var f = (d - target) * k;
            var fx = dx / d * f;
            var fy = dy / d * f;
            e.A.Vx += fx; e.A.Vy += fy;
            e.B.Vx -= fx; e.B.Vy -= fy;
        }

        // 중심 인력 + 적분
        var cx = Math.Max(Bounds.Width, 600) / 2;
        var cy = Math.Max(Bounds.Height, 400) / 2;
        foreach (var n in _nodes)
        {
            n.Vx += (cx - n.X) * 0.003 * alpha;
            n.Vy += (cy - n.Y) * 0.003 * alpha;
            if (!n.Pinned)
            {
                n.X += Math.Clamp(n.Vx, -12, 12);
                n.Y += Math.Clamp(n.Vy, -12, 12);
            }
            n.Vx *= 0.82;
            n.Vy *= 0.82;
        }
        InvalidateVisual();
    }

    // ── 좌표 변환 ────────────────────────────────────────────────────
    private Point ToScreen(double x, double y) => new(x * _zoom + _pan.X, y * _zoom + _pan.Y);
    private Point ToWorld(Point p) => new((p.X - _pan.X) / _zoom, (p.Y - _pan.Y) / _zoom);

    // ── 렌더링 ───────────────────────────────────────────────────────
    private IBrush BrushFor(string key) =>
        this.FindResource(ActualThemeVariant, key) is Color c ? new SolidColorBrush(c) : Brushes.Gray;

    private IBrush TypeBrush(string type) => BrushFor(type switch
    {
        NodeTypes.Manual => "TypeManualColor",
        NodeTypes.Procedure => "TypeProcedureColor",
        NodeTypes.Sop => "TypeSopColor",
        NodeTypes.WorkStandard => "TypeWorkStdColor",
        NodeTypes.Form => "TypeFormColor",
        NodeTypes.Standard => "TypeStandardColor",
        NodeTypes.Regulation => "TypeRegulationColor",
        NodeTypes.TechDoc => "TypeTechDocColor",
        _ => "Ink3Color",
    });

    public override void Render(DrawingContext ctx)
    {
        ctx.FillRectangle(BrushFor("Surface2Color"), new Rect(Bounds.Size));
        if (_nodes.Count == 0) return;

        var edgeBrush = BrushFor("EdgeLineColor");
        var accent = BrushFor("AccentColor");
        var crit = BrushFor("CritColor");
        var ink = BrushFor("InkColor");
        var surface = BrushFor("SurfaceColor");
        var hasSelection = _selected is not null;

        foreach (var e in _edges)
        {
            var involved = hasSelection &&
                           (e.A.Doc.Id == _selected!.Doc.Id || e.B.Doc.Id == _selected.Doc.Id);
            var opacity = !hasSelection ? 0.45 : involved ? 0.95 : 0.06;
            var brush = e.Doc.Type == EdgeTypes.Process ? accent : edgeBrush;
            var thickness = (e.Doc.Type == EdgeTypes.Hierarchy ? 1.0 : 1.4) * (involved ? 1.6 : 1.0);
            var pen = new Pen(brush, thickness)
            {
                DashStyle = e.Doc.Type is EdgeTypes.External or EdgeTypes.Derived ? DashStyle.Dash : null,
            };
            using (ctx.PushOpacity(opacity))
                ctx.DrawLine(pen, ToScreen(e.A.X, e.A.Y), ToScreen(e.B.X, e.B.Y));
        }

        var typeface = new Typeface(new FontFamily("Malgun Gothic, Apple SD Gothic Neo, Noto Sans CJK KR, sans-serif"));
        foreach (var n in _nodes)
        {
            var isSel = hasSelection && n.Doc.Id == _selected!.Doc.Id;
            var isNeighbor = hasSelection && _neighborIds.Contains(n.Doc.Id);
            var opacity = !hasSelection || isSel || isNeighbor ? 1.0 : 0.15;
            var p = ToScreen(n.X, n.Y);
            var r = n.R * _zoom;

            using (ctx.PushOpacity(opacity))
            {
                if (isSel)
                    ctx.DrawEllipse(null, new Pen(accent, 2.5), p, r + 5, r + 5);
                var stroke = n.Doc.Gap == true ? new Pen(crit, 2) { DashStyle = DashStyle.Dash } : new Pen(surface, 1.2);
                ctx.DrawEllipse(TypeBrush(n.Doc.Type), stroke, p, r, r);

                var showLabel = n.Doc.Type is NodeTypes.Manual or NodeTypes.Procedure
                    or NodeTypes.Regulation or NodeTypes.Standard or NodeTypes.TechDoc || isSel || isNeighbor;
                if (showLabel && _zoom > 0.45)
                {
                    var name = n.Doc.Name.Length > 14 ? n.Doc.Name[..13] + "…" : n.Doc.Name;
                    var ft = new FormattedText(name, System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight, typeface,
                        n.Doc.Type == NodeTypes.Manual ? 12.5 : 10.5, ink);
                    ctx.DrawText(ft, new Point(p.X - ft.Width / 2, p.Y - r - ft.Height - 3));
                }
            }
        }
    }

    // ── 인터랙션 ─────────────────────────────────────────────────────
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pos = e.GetPosition(this);
        _lastPointer = pos;
        var hit = HitTestNode(pos);
        if (hit is not null)
        {
            _dragNode = hit;
            hit.Pinned = true;
            _ticksLeft = Math.Max(_ticksLeft, 60);
            _timer.Start();
        }
        else
        {
            _dragBackgroundFrom = pos;
        }
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pos = e.GetPosition(this);
        if (_dragNode is not null)
        {
            var world = ToWorld(pos);
            _dragNode.X = world.X;
            _dragNode.Y = world.Y;
            _ticksLeft = Math.Max(_ticksLeft, 40);
            if (!_timer.IsEnabled) _timer.Start();
            InvalidateVisual();
        }
        else if (_dragBackgroundFrom is not null)
        {
            _pan = new Point(_pan.X + pos.X - _lastPointer.X, _pan.Y + pos.Y - _lastPointer.Y);
            InvalidateVisual();
        }
        _lastPointer = pos;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var pos = e.GetPosition(this);
        if (_dragNode is not null)
        {
            _dragNode.Pinned = false;
            // 이동량이 작으면 클릭으로 간주 → 선택
            Select(_dragNode);
            _dragNode = null;
        }
        else if (_dragBackgroundFrom is { } from &&
                 Math.Abs(pos.X - from.X) < 4 && Math.Abs(pos.Y - from.Y) < 4)
        {
            Select(null); // 배경 클릭 = 해제
        }
        _dragBackgroundFrom = null;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var pos = e.GetPosition(this);
        var world = ToWorld(pos);
        _zoom = Math.Clamp(_zoom * (e.Delta.Y > 0 ? 1.12 : 1 / 1.12), 0.2, 5);
        _pan = new Point(pos.X - world.X * _zoom, pos.Y - world.Y * _zoom);
        InvalidateVisual();
        e.Handled = true;
    }

    private VNode? HitTestNode(Point screen)
    {
        var world = ToWorld(screen);
        VNode? best = null;
        var bestD = double.MaxValue;
        foreach (var n in _nodes)
        {
            var dx = n.X - world.X;
            var dy = n.Y - world.Y;
            var d = Math.Sqrt(dx * dx + dy * dy);
            var hitR = n.R + 5 / _zoom;
            if (d < hitR && d < bestD) { best = n; bestD = d; }
        }
        return best;
    }

    private void Select(VNode? node)
    {
        _selected = node;
        _neighborIds = node is null || _net is null
            ? new HashSet<string>()
            : _net.Neighbors(node.Doc.Id).Select(x => x.Other.Id).ToHashSet();
        _onSelect?.Invoke(node?.Doc);
        InvalidateVisual();
    }
}
