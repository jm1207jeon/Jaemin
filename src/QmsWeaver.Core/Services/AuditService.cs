using System.Text.Json;

namespace QmsWeaver.Core.Services;

/// <summary>
/// EDMS 감사 추적(audit trail): 추가 전용 JSONL.
/// 조회·검토·스캔·설정변경 등 사용자 행위를 시각과 함께 기록한다.
/// </summary>
public sealed class AuditService
{
    private readonly string _path;
    private readonly object _gate = new();

    public AuditService(string dir)
    {
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "audit.jsonl");
    }

    public void Log(string action, string? target = null, string? detail = null)
    {
        var line = JsonSerializer.Serialize(new
        {
            ts = DateTime.UtcNow.ToString("o"),
            user = Environment.UserName,
            action,
            target,
            detail,
        });
        lock (_gate) File.AppendAllText(_path, line + Environment.NewLine);
    }

    public IReadOnlyList<string> Tail(int count = 200)
    {
        lock (_gate)
        {
            if (!File.Exists(_path)) return Array.Empty<string>();
            var lines = File.ReadAllLines(_path);
            return lines.Skip(Math.Max(0, lines.Length - count)).ToArray();
        }
    }
}
