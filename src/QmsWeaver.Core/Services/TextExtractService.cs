using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace QmsWeaver.Core.Services;

/// <summary>기록 파일에서 본문 텍스트를 추출한다 (정합성 점검·AI 분석 입력).</summary>
public static partial class TextExtractService
{
    [GeneratedRegex("<[^>]+>")] private static partial Regex TagRx();

    public static string? TryExtract(string filePath, int maxChars = 60_000)
    {
        try
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            var text = ext switch
            {
                ".txt" => File.ReadAllText(filePath),
                ".docx" => ExtractDocx(filePath),
                ".xlsx" or ".xlsm" => ExtractXlsx(filePath),
                _ => null, // pdf/hwp는 v1에서 미지원 — 수동 확인 안내
            };
            if (text is null) return null;
            text = Regex.Replace(text, @"\s+", " ").Trim();
            return text.Length > maxChars ? text[..maxChars] : text;
        }
        catch
        {
            return null;
        }
    }

    private static string ExtractDocx(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        var entry = zip.GetEntry("word/document.xml")
                    ?? throw new InvalidDataException("document.xml 없음");
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        var xml = reader.ReadToEnd();
        // 단락 경계를 공백으로 유지한 채 태그 제거
        xml = xml.Replace("</w:p>", " \n");
        return TagRx().Replace(xml, "");
    }

    private static string ExtractXlsx(string path)
    {
        using var wb = new XLWorkbook(path);
        var sb = new StringBuilder();
        foreach (var ws in wb.Worksheets)
            foreach (var cell in ws.CellsUsed())
                sb.Append(cell.GetString()).Append(' ');
        return sb.ToString();
    }
}
