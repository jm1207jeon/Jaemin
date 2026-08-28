using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using QmsWeaver.Core.Models;
using QmsWeaver.Core.Services;

namespace QmsWeaver.App.Controls;

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
    private VNode? _hover;
    private HashSet<string> _neighborIds = new();
    private bool _didInitialFit;

    /// <summary>레이아웃 고정 — 시뮬레이션 정지 (멘탈맵 유지용).</summary>
    public bool LayoutFrozen { get; set; }

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
            else (node.X, node.Y) = InitialPosition(doc, w, h);
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
        if (!LayoutFrozen) _timer.Start();
        InvalidateVisual();
    }

    /// <summary>
    /// 결정적 초기 배치 — 멘탈맵 안정성(같은 데이터 = 항상 같은 지형).
    /// ISO 13485 조항(4~8)별 방사형 섹터: 매뉴얼 중심, 절차서는 조항 섹터의 링,
    /// 하위 문서는 상위 근처, 규격·규제는 연결 절차 방향의 외곽 링.
    /// </summary>
    private (double X, double Y) InitialPosition(DocNode doc, double w, double h)
    {
        var cx = w / 2;
        var cy = h / 2;
        double Angle(int clause, string id)
        {
            // 조항 4~8 → 5개 섹터(72°). 섹터 내에서는 ID 해시로 분산.
            var sector = Math.Clamp(clause - 4, 0, 4);
            var jitter = (StableHash(id) % 1000) / 1000.0; // 0~1
            return (sector + 0.1 + jitter * 0.8) / 5.0 * Math.Tau - Math.PI / 2;
        }

        if (doc.Type == NodeTypes.Manual) return (cx, cy);

        if (doc.Type == NodeTypes.Procedure && doc.Clause is int c)
        {
            var a = Angle(c, doc.Id);
            return (cx + Math.Cos(a) * 150, cy + Math.Sin(a) * 150);
        }

        if (doc.Type is NodeTypes.Standard or NodeTypes.Regulation or NodeTypes.TechDoc)
        {
            // 연결된 첫 절차서의 각도 방향 외곽에 배치
            var linked = _net!.Neighbors(doc.Id)
                .Select(x => x.Other)
                .FirstOrDefault(n => n.Type == NodeTypes.Procedure && n.Clause is not null);
            var a = linked?.Clause is int lc
                ? Angle(lc, doc.Id)
                : (StableHash(doc.Id) % 1000) / 1000.0 * Math.Tau;
            var radius = doc.Type == NodeTypes.TechDoc ? 300 : 360;
            return (cx + Math.Cos(a) * radius, cy + Math.Sin(a) * radius);
        }

        // 지침/작업표준/양식: 상위 문서 근처
        var parentId = HierarchyService.ParentOf(doc.Id);
        if (parentId is not null && _byId.TryGetValue(parentId, out var parent))
        {
            var ja = (StableHash(doc.Id) % 1000) / 1000.0 * Math.Tau;
            var jr = doc.Type == NodeTypes.Form ? 55 : 40;
            return (parent.X + Math.Cos(ja) * jr, parent.Y + Math.Sin(ja) * jr);
        }
        if (doc.Clause is int c2)
        {
            var a = Angle(c2, doc.Id);
            return (cx + Math.Cos(a) * 230, cy + Math.Sin(a) * 230);
        }
        return (cx + (_rng.NextDouble() - 0.5) * 200, cy + (_rng.NextDouble() - 0.5) * 200);
    }

    private static int StableHash(string s)
    {
        unchecked
        {
            var h = 23;
            foreach (var ch in s) h = h * 31 + ch;
            return Math.Abs(h);
        }
    }

    /// <summary>전체 노드가 보이도록 줌·팬 조정 (Overview first).</summary>
    public void FitToView()
    {
        if (_nodes.Count == 0 || Bounds.Width < 10) return;
        var minX = _nodes.Min(n => n.X) - 60;
        var maxX = _nodes.Max(n => n.X) + 60;
        var minY = _nodes.Min(n => n.Y) - 60;
        var maxY = _nodes.Max(n => n.Y) + 60;
        var zx = Bounds.Width / Math.Max(1, maxX - minX);
        var zy = Bounds.Height / Math.Max(1, maxY - minY);
        _zoom = Math.Clamp(Math.Min(zx, zy), 0.2, 1.6);
        _pan = new Point(
            (Bounds.Width - (minX + maxX) * _zoom) / 2,
            (Bounds.Height - (minY + maxY) * _zoom) / 2);
        InvalidateVisual();
    }

    /// <summary>특정 노드를 화면 중앙으로 (검색 → 위치 이동).</summary>
    public void CenterOn(string nodeId)
    {
        if (!_byId.TryGetValue(nodeId, out var n)) return;
        if (_zoom < 0.8) _zoom = 1.0;
        _pan = new Point(Bounds.Width / 2 - n.X * _zoom, Bounds.Height / 2 - n.Y * _zoom);
        InvalidateVisual();
    }

    public void SetFrozen(bool frozen)
    {
        LayoutFrozen = frozen;
        if (frozen) _timer.Stop();
        else { _ticksLeft = Math.Max(_ticksLeft, 80); _timer.Start(); }
    }

    // ── 물리 시뮬레이션 ───────────────────────────────────────────────
    private void Tick()
    {
        if (LayoutFrozen) { _timer.Stop(); return; }
        if (_ticksLeft-- <= 0)
        {
            _timer.Stop();
            if (!_didInitialFit) { _didInitialFit = true; FitToView(); }
            return;
        }
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
    private Color ColorFor(string key) =>
        this.FindResource(ActualThemeVariant, key) is Color c ? c : Colors.Gray;

    private IBrush BrushFor(string key) => new SolidColorBrush(ColorFor(key));

    /// <summary>
    /// 색상 체계: ISO 13485 조항(4~8)이 대분류 색을 결정하고,
    /// 같은 조항에 종속된 지침/작업표준/양식은 그 색의 밝은 변형을 갖는다 (색 가족).
    /// 매뉴얼·규격·규제·기술문서는 별도의 중립 계열.
    /// </summary>
    private IBrush NodeBrush(DocNode doc)
    {
        switch (doc.Type)
        {
            case NodeTypes.Manual: return BrushFor("NodeManualColor");
            case NodeTypes.Standard: return BrushFor("NodeStandardColor");
            case NodeTypes.Regulation: return BrushFor("NodeRegulationColor");
            case NodeTypes.TechDoc: return BrushFor("NodeTechDocColor");
        }
        var baseColor = ColorFor(doc.Clause switch
        {
            4 => "Clause4Color",
            5 => "Clause5Color",
            6 => "Clause6Color",
            7 => "Clause7Color",
            8 => "Clause8Color",
            _ => "Ink3Color",
        });
        // 하위 계층일수록 밝고 옅게 — 절차서(기본) → 지침·작업표준 → 양식
        var lighten = doc.Type switch
        {
            NodeTypes.Sop or NodeTypes.WorkStandard => 0.22,
            NodeTypes.Form => 0.42,
            _ => 0.0,
        };
        return new SolidColorBrush(Lighten(baseColor, lighten));
    }

    private Color Lighten(Color c, double amount)
    {
        if (amount <= 0) return c;
        // 다크 테마에선 배경이 어두우므로 반대로 어둡게 내려 가족 관계를 유지
        var dark = ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;
        double Mix(double ch, double target) => ch + (target - ch) * amount;
        var target = dark ? 30.0 : 255.0;
        return Color.FromRgb(
            (byte)Mix(c.R, target), (byte)Mix(c.G, target), (byte)Mix(c.B, target));
    }

    /// <summary>
    /// 문서 성격별 노드 형상 — 색과 독립적인 2차 시각 채널 (색각 이상·흑백 인쇄 대응).
    /// 매뉴얼=이중 링 원, 절차서=원, 지침·작업표준=둥근 사각, 양식=다이아몬드,
    /// 규격=삼각형(▲), 규제=역삼각형(▼), 기술문서=육각형.
    /// </summary>
    private static void DrawNodeShape(DrawingContext ctx, string type, Point p, double r, IBrush fill, Pen stroke)
    {
        switch (type)
        {
            case NodeTypes.Manual:
                ctx.DrawEllipse(fill, stroke, p, r, r);
                ctx.DrawEllipse(null, stroke, p, r + 3, r + 3);
                break;
            case NodeTypes.Procedure:
                ctx.DrawEllipse(fill, stroke, p, r, r);
                break;
            case NodeTypes.Sop or NodeTypes.WorkStandard:
                ctx.DrawRectangle(fill, stroke,
                    new RoundedRect(new Rect(p.X - r, p.Y - r, r * 2, r * 2), r * 0.35));
                break;
            case NodeTypes.Form:
                DrawPolygon(ctx, fill, stroke,
                    new[] { new Point(p.X, p.Y - r), new Point(p.X + r, p.Y), new Point(p.X, p.Y + r), new Point(p.X - r, p.Y) });
                break;
            case NodeTypes.Standard:
                DrawPolygon(ctx, fill, stroke,
                    new[] { new Point(p.X, p.Y - r), new Point(p.X + r * 0.95, p.Y + r * 0.75), new Point(p.X - r * 0.95, p.Y + r * 0.75) });
                break;
            case NodeTypes.Regulation:
                DrawPolygon(ctx, fill, stroke,
                    new[] { new Point(p.X - r * 0.95, p.Y - r * 0.75), new Point(p.X + r * 0.95, p.Y - r * 0.75), new Point(p.X, p.Y + r) });
                break;
            case NodeTypes.TechDoc:
                var pts = new Point[6];
                for (var i = 0; i < 6; i++)
                {
                    var a = Math.PI / 3 * i - Math.PI / 6;
                    pts[i] = new Point(p.X + Math.Cos(a) * r, p.Y + Math.Sin(a) * r);
                }
                DrawPolygon(ctx, fill, stroke, pts);
                break;
            default:
                ctx.DrawEllipse(fill, stroke, p, r, r);
                break;
        }
    }

    private static void DrawPolygon(DrawingContext ctx, IBrush fill, Pen stroke, Point[] pts)
    {
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(pts[0], true);
            for (var i = 1; i < pts.Length; i++) g.LineTo(pts[i]);
            g.EndFigure(true);
        }
        ctx.DrawGeometry(fill, stroke, geo);
    }

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
            var isHover = _hover == n;
            var opacity = !hasSelection || isSel || isNeighbor ? 1.0 : (isHover ? 0.6 : 0.15);
            var p = ToScreen(n.X, n.Y);
            var r = n.R * _zoom;

            using (ctx.PushOpacity(opacity))
            {
                if (isSel)
                    ctx.DrawEllipse(null, new Pen(accent, 2.5), p, r + 6, r + 6);
                else if (isHover)
                    ctx.DrawEllipse(null, new Pen(accent, 1.5), p, r + 4.5, r + 4.5);
                var stroke = n.Doc.Gap == true ? new Pen(crit, 2) { DashStyle = DashStyle.Dash } : new Pen(surface, 1.2);
                DrawNodeShape(ctx, n.Doc.Type, p, r, NodeBrush(n.Doc), stroke);

                // 시맨틱 줌: 항상 라벨(주요 타입) + 선택·이웃·호버 시 라벨, 줌인하면 전체
                var showLabel = n.Doc.Type is NodeTypes.Manual or NodeTypes.Procedure
                    or NodeTypes.Regulation or NodeTypes.Standard or NodeTypes.TechDoc
                    || isSel || isNeighbor || isHover || _zoom > 1.6;
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
        else
        {
            var hover = HitTestNode(pos);
            if (hover != _hover)
            {
                _hover = hover;
                Cursor = hover is null ? Cursor.Default : new Cursor(StandardCursorType.Hand);
                InvalidateVisual();
            }
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
