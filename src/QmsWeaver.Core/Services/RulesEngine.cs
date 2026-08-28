using QmsWeaver.Core.Models;

namespace QmsWeaver.Core.Services;

/// <summary>
/// 정합성 점검(검토하기)과 개정 영향 분석(체크리스트 생성)의 규칙 기반 엔진.
/// AI 보조 점검은 AiService가 이 결과 위에 추가한다.
/// </summary>
public sealed class RulesEngine
{
    private readonly SeedNetworkService _net;
    public RulesEngine(SeedNetworkService net) => _net = net;

    /// <summary>기록 파일 본문 텍스트(추출 가능할 때)와 메타데이터로 정합성을 점검한다.</summary>
    public List<ReviewFinding> Review(RecordEntry record, string? bodyText, IReadOnlyList<RecordEntry> siblings)
    {
        var findings = new List<ReviewFinding>();

        // 1) 서명 완결성
        findings.Add(record.Status switch
        {
            RecordStatus.Approved => Pass("서명 완결", "작성·검토·승인 서명 확인"),
            RecordStatus.MissingApproval => Fail("승인 서명 미완", "승인란 공란 — 기록관리(QP-402) 완결성 요건 미충족"),
            RecordStatus.InReview => Warn("검토 진행중", "검토/승인 서명이 아직 완료되지 않음"),
            _ => Warn("서명 정보 없음", "파일에서 작성/검토/승인자를 추출하지 못함 — 직접 확인 필요"),
        });

        // 2) 인용 문서번호·Rev 정합 (본문 텍스트가 있을 때)
        if (!string.IsNullOrWhiteSpace(bodyText))
        {
            foreach (var (docId, citedRev) in HierarchyService.ExtractCitations(bodyText).DistinctBy(c => c.DocId))
            {
                if (!_net.NodesById.TryGetValue(docId, out var node))
                {
                    findings.Add(Warn("미등록 문서 인용", $"{docId} — 문서 등록 대장에 없는 번호를 인용"));
                    continue;
                }
                if (citedRev is int cr && int.TryParse(node.Rev, out var currentRev) && cr != currentRev)
                    findings.Add(Fail("인용 개정번호 불일치",
                        $"{docId} Rev.{cr} 인용 — 현행 Rev.{currentRev} ({node.Effective} 적용). 개정 반영 여부 확인 필요"));
            }
        }
        else
        {
            findings.Add(Warn("본문 미추출", "파일 본문을 추출하지 못해 인용 Rev·판정기준 점검은 수동 확인 필요"));
        }

        // 3) 주기성: 같은 그룹의 직전 기록과의 간격 (밸리데이션류 12개월 규칙)
        var prev = siblings
            .Where(s => s.FilePath != record.FilePath && s.Group == record.Group && s.PerformedDate < record.PerformedDate)
            .OrderByDescending(s => s.PerformedDate)
            .FirstOrDefault();
        if (prev?.PerformedDate is { } p && record.PerformedDate is { } cur)
        {
            var months = (cur.Year - p.Year) * 12 + cur.Month - p.Month;
            findings.Add(months <= 13
                ? Pass("수행 주기", $"전회 {p:yyyy-MM} → {months}개월 간격")
                : Warn("수행 주기 확인", $"전회 {p:yyyy-MM}에서 {months}개월 경과 — 주기 규정 확인 필요"));
        }

        // 4) 파일 관리 상태
        if (record.Rev is null)
            findings.Add(Warn("Rev 미표기", "파일명/메타데이터에서 개정번호를 찾지 못함 — 명명규칙(QP-401) 확인"));

        return findings;
    }

    /// <summary>노드(문서) 개정 시 동반 산출물 체크리스트. 엣지의 영향 전파 규칙 기반.</summary>
    public List<ImpactItem> ImpactOfRevision(string nodeId)
    {
        var items = new List<ImpactItem>
        {
            new() { Action = "품질문서(제/개/폐) 신청서 작성", Basis = "F401-08 · QP-401" },
            new() { Action = "문서 등록 이력 대장 갱신", Basis = "F401-09" },
            new() { Action = "구버전 배포본 회수 기록", Basis = "QP-401" },
            new() { Action = "교육훈련 실시 및 결과보고서", Basis = "F601-03 · 관련 작업자" },
        };

        if (!_net.NodesById.TryGetValue(nodeId, out var node)) return items;

        // 작업표준/지침 개정 → 적격성 재평가
        if (node.Type is NodeTypes.Sop or NodeTypes.WorkStandard)
            items.Add(new() { Action = "적격성인정평가서 재평가", Basis = "F601-08 · 해당 시" });

        // 공정/밸리데이션 계열 → 재밸리데이션 검토 + 위험관리파일
        if (nodeId.Contains("705") || nodeId.Contains("706"))
        {
            items.Add(new() { Action = "재밸리데이션 필요성 평가", Basis = "QP-706" });
            items.Add(new() { Action = "밸리데이션 검토/생산 승인 재발행", Basis = "F706-02" });
            items.Add(new() { Action = "위험관리파일 영향 검토", Basis = "QP-701 · 공정 파라미터 변경 시" });
        }

        // 설계 관련 → 설계변경 절차
        if (nodeId.Contains("703"))
            items.Add(new() { Action = "설계변경(ECR/ECO) 검토", Basis = "F703-01/02 · QP-703" });

        // 라벨/포장 → 각국 허가 영향
        if (nodeId.Contains("709"))
            items.Add(new() { Action = "각국 허가 변경신고 필요성 검토", Basis = "QP-813~819 · RA" });

        // 규제요구 엣지가 있으면 해당 규제 대응 확인
        foreach (var (other, edge) in _net.Neighbors(nodeId).Where(x => x.Edge.Type == EdgeTypes.External))
            items.Add(new() { Action = $"{other.Name} 요구사항 재확인", Basis = other.Id });

        return items.DistinctBy(i => i.Action).ToList();
    }

    private static ReviewFinding Pass(string t, string d) => new() { Severity = ReviewSeverity.Pass, Title = t, Detail = d };
    private static ReviewFinding Warn(string t, string d) => new() { Severity = ReviewSeverity.Warning, Title = t, Detail = d };
    private static ReviewFinding Fail(string t, string d) => new() { Severity = ReviewSeverity.Fail, Title = t, Detail = d };
}
