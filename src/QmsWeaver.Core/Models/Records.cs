namespace QmsWeaver.Core.Models;

/// <summary>폴더 스캔으로 발견된 실제 수행 기록(파일) 1건.</summary>
public sealed class RecordEntry
{
    public string FilePath { get; set; } = "";
    public string FileName { get; set; } = "";
    /// <summary>귀속된 문서 노드 ID (예: QP-706). 매칭 실패 시 null → 미분류 인박스.</summary>
    public string? NodeId { get; set; }
    /// <summary>중간 그룹 (예: "열처리") — 폴더 경로에서 유도.</summary>
    public string? Group { get; set; }
    /// <summary>실행 단위 (예: "2025") — 폴더/파일명에서 유도.</summary>
    public string? Unit { get; set; }
    public string Title { get; set; } = "";
    public int? Rev { get; set; }
    public DateOnly? PerformedDate { get; set; }
    public string? Author { get; set; }
    public string? Reviewer { get; set; }
    public string? Approver { get; set; }
    public long SizeBytes { get; set; }
    public DateTime ModifiedUtc { get; set; }

    /// <summary>같은 문서의 여러 개정본 중 최신본인가. 구버전도 목록에 남아 조회 가능하다.</summary>
    public bool IsCurrent { get; set; } = true;

    /// <summary>대표 파일 형식 (DOCX/PDF/XLSX/...). 같은 기록에 docx·pdf가 함께 있으면 docx가 대표.</summary>
    public string Format => Path.GetExtension(FilePath).TrimStart('.').ToUpperInvariant();

    /// <summary>대표 형식 외에 함께 존재하는 형식들 (예: docx 대표 + PDF 사본).</summary>
    public List<string> AltFormats { get; set; } = new();

    /// <summary>테이블 표시용: "DOCX (+PDF)" 형태.</summary>
    public string FormatDisplay =>
        AltFormats.Count == 0 ? Format : $"{Format} (+{string.Join(",", AltFormats)})";

    /// <summary>버전 그룹핑 키 계산용 — Rev/일자/확장자를 제거한 정규화 제목.</summary>
    public string NormalizedTitle { get; set; } = "";

    public RecordStatus Status
    {
        get
        {
            var signed = new[] { Author, Reviewer, Approver }.Count(s => !string.IsNullOrWhiteSpace(s));
            return signed switch
            {
                3 => RecordStatus.Approved,
                0 => RecordStatus.Draft,
                _ when string.IsNullOrWhiteSpace(Approver) => RecordStatus.MissingApproval,
                _ => RecordStatus.InReview,
            };
        }
    }
}

public enum RecordStatus
{
    Draft,
    InReview,
    MissingApproval,
    Approved,
}

public static class RecordStatusExtensions
{
    public static string Label(this RecordStatus s) => s switch
    {
        RecordStatus.Approved => "승인완료",
        RecordStatus.MissingApproval => "승인 서명 누락",
        RecordStatus.InReview => "검토중",
        _ => "작성중",
    };
}

/// <summary>노드(또는 전역)와 실제 폴더의 연결.</summary>
public sealed class FolderBinding
{
    /// <summary>null이면 통합 루트(전역 규칙 매칭).</summary>
    public string? NodeId { get; set; }
    public string Path { get; set; } = "";
    public bool Recursive { get; set; } = true;
}

public sealed class ReviewFinding
{
    public ReviewSeverity Severity { get; set; }
    public string Title { get; set; } = "";
    public string Detail { get; set; } = "";
    /// <summary>AI 보조 점검에서 생성되었는지 여부.</summary>
    public bool FromAi { get; set; }
}

public enum ReviewSeverity { Pass, Warning, Fail }

public sealed class ImpactItem
{
    public string Action { get; set; } = "";
    public string Basis { get; set; } = "";
    public bool Done { get; set; }
}
