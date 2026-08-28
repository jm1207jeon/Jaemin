using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using QmsAnalyzer.Core.Models;

namespace QmsAnalyzer.Core.Services;

/// <summary>
/// Claude API 기반 AI 보조 분석.
/// - 기록 vs 절차/지침/규격 문안 대조 (검토하기 보강)
/// - 문서 요약
/// 키는 설정 화면에서 입력되어 로컬 config.json에만 저장된다.
/// </summary>
public sealed class AiService
{
    private readonly Func<AiSettings> _settings;

    public AiService(Func<AiSettings> settings) => _settings = settings;

    public bool IsConfigured => _settings().Enabled;

    private AnthropicClient CreateClient()
    {
        var s = _settings();
        var client = new AnthropicClient { ApiKey = s.ApiKey };
        if (!string.IsNullOrWhiteSpace(s.BaseUrl) &&
            !string.Equals(s.BaseUrl, "https://api.anthropic.com", StringComparison.OrdinalIgnoreCase))
        {
            client = new AnthropicClient { ApiKey = s.ApiKey, BaseUrl = s.BaseUrl };
        }
        return client;
    }

    /// <summary>연결 테스트: 최소 호출로 키·모델 유효성만 확인한다.</summary>
    public async Task<(bool Ok, string Message)> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var client = CreateClient();
            var response = await client.Messages.Create(new MessageCreateParams
            {
                Model = _settings().Model,
                MaxTokens = 32,
                Messages = [new() { Role = Role.User, Content = "ping — 한 단어로만 답하세요." }],
            }, cancellationToken: ct);
            return (true, $"연결 성공 · 모델 {_settings().Model}");
        }
        catch (Exception ex)
        {
            return (false, $"연결 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 기록 본문을 관련 절차/지침 발췌와 대조하여 불일치 후보를 찾는다.
    /// 결과는 규칙 기반 점검 결과 뒤에 FromAi=true로 덧붙인다.
    /// </summary>
    public async Task<List<ReviewFinding>> ReviewAgainstReferencesAsync(
        string recordTitle, string recordBody,
        IReadOnlyList<(string DocId, string DocName, string Excerpt)> references,
        CancellationToken ct = default)
    {
        if (!IsConfigured) return new();

        var refText = string.Join("\n\n", references.Select(r =>
            $"### {r.DocId} {r.DocName}\n{Truncate(r.Excerpt, 8000)}"));

        var prompt = $$"""
            당신은 의료기기 QMS(ISO 13485) 심사 전문가입니다.
            아래 [기록]이 [상위 문서]들의 규정과 일치하는지 점검하세요.

            점검 관점: 판정기준·허용범위 수치 불일치, 요구 항목 누락, 절차 순서 위반,
            인용 문서 개정번호 불일치, 규격 필수 파라미터 누락.

            반드시 아래 JSON 배열만 출력하세요 (다른 텍스트 금지):
            [{"severity":"fail|warning|pass","title":"짧은 제목","detail":"근거와 위치를 포함한 설명"}]
            문제가 없으면 pass 항목 1개로 요약하세요.

            [기록: {{recordTitle}}]
            {{Truncate(recordBody, 30000)}}

            [상위 문서]
            {{refText}}
            """;

        try
        {
            var client = CreateClient();
            var response = await client.Messages.Create(new MessageCreateParams
            {
                Model = _settings().Model,
                MaxTokens = 4096,
                Messages = [new() { Role = Role.User, Content = prompt }],
            }, cancellationToken: ct);

            var text = string.Concat(response.Content
                .Select(b => b.Value)
                .OfType<TextBlock>()
                .Select(t => t.Text));
            return ParseFindings(text);
        }
        catch (Exception ex)
        {
            return new()
            {
                new ReviewFinding
                {
                    Severity = ReviewSeverity.Warning,
                    Title = "AI 점검 실패",
                    Detail = ex.Message,
                    FromAi = true,
                },
            };
        }
    }

    /// <summary>문서 요약 (라이브러리 카드·상세 패널용).</summary>
    public async Task<string?> SummarizeAsync(string title, string body, CancellationToken ct = default)
    {
        if (!IsConfigured) return null;
        try
        {
            var client = CreateClient();
            var response = await client.Messages.Create(new MessageCreateParams
            {
                Model = _settings().Model,
                MaxTokens = 1024,
                Messages =
                [
                    new()
                    {
                        Role = Role.User,
                        Content = $"다음 의료기기 QMS 문서를 2~3문장으로 요약하세요. 목적·범위·핵심 요구사항 중심으로.\n\n[{title}]\n{Truncate(body, 30000)}",
                    },
                ],
            }, cancellationToken: ct);
            return string.Concat(response.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text)).Trim();
        }
        catch
        {
            return null;
        }
    }

    internal static List<ReviewFinding> ParseFindings(string text)
    {
        // 모델이 코드펜스로 감싸는 경우 방어
        var start = text.IndexOf('[');
        var end = text.LastIndexOf(']');
        if (start < 0 || end <= start) return new();
        try
        {
            using var doc = JsonDocument.Parse(text[start..(end + 1)]);
            var list = new List<ReviewFinding>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var sev = el.TryGetProperty("severity", out var s) ? s.GetString() : "warning";
                list.Add(new ReviewFinding
                {
                    Severity = sev switch
                    {
                        "fail" => ReviewSeverity.Fail,
                        "pass" => ReviewSeverity.Pass,
                        _ => ReviewSeverity.Warning,
                    },
                    Title = el.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "",
                    Detail = el.TryGetProperty("detail", out var d) ? d.GetString() ?? "" : "",
                    FromAi = true,
                });
            }
            return list;
        }
        catch
        {
            return new();
        }
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
