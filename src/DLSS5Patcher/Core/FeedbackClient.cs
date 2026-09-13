using System.Text;
using System.Text.Json;

namespace DLSS5Patcher.Core;

/// <summary>反馈提交客户端：组装 JSON 载荷（环境 + 勾选游戏 + 日志 + 截图 + 可选用户名）并 POST 到分发服务器；
/// 提交成功取得反馈码，用户可凭码随时查询处理进度与管理员回复（同 GSX）。</summary>
public static class FeedbackClient
{
    public sealed record GameLine(string Name, string Dir, string Source, string State);
    public sealed record LogEntry(string Name, long Size, bool Truncated, string Content);
    public sealed record ShotEntry(string Name, long Size, byte[] Data);
    public sealed record Report(
        string Version, string Os, string Runtime, GpuInfo Gpu,
        List<GameLine> Games, string Description, string Username,
        List<LogEntry> Logs, List<ShotEntry> Shots);

    public sealed record QueryResult(string StatusCode, string CreatedAt, string Username, string AdminReply);

    /// <summary>读取文本文件尾部（大日志只取末尾 maxBytes，避免反馈载荷过大）。</summary>
    public static (string content, long size, bool truncated) ReadTail(string path, int maxBytes = 262_144)
    {
        var fi = new FileInfo(path);
        if (!fi.Exists) throw new FileNotFoundException(path);
        if (fi.Length <= maxBytes)
            return (File.ReadAllText(path), fi.Length, false);
        using var fs = File.OpenRead(path);
        fs.Seek(-maxBytes, SeekOrigin.End);
        using var reader = new StreamReader(fs, Encoding.UTF8);
        var text = reader.ReadToEnd();
        return (L.S($"（日志过大，已截断，仅保留末尾 {maxBytes / 1024} KB）\r\n", $"(Log too large — truncated to the last {maxBytes / 1024} KB)\r\n") + text,
            fi.Length, true);
    }

    public static async Task<(string Id, string Code)> SubmitAsync(Report report, CancellationToken ct = default)
    {
        var body = BuildJson(report);
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"DLSS5Patcher/{Updater.CurrentVersion}");
        using var content = new ByteArrayContent(body);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        using var resp = await http.PostAsync($"{PackageCatalog.ServerBase}/feedback", content, ct);
        var text = await resp.Content.ReadAsStringAsync(ct);
        string? id = null, code = null, error = null;
        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            id = root.TryGetProperty("id", out var i) ? i.GetString() : null;
            code = root.TryGetProperty("feedbackCode", out var fc) ? fc.GetString() : null;
            error = root.TryGetProperty("error", out var e) ? e.GetString() : null;
        }
        catch { }

        if (resp.IsSuccessStatusCode && !string.IsNullOrEmpty(id)) return (id, code ?? "");
        throw new InvalidOperationException(
            !string.IsNullOrEmpty(error) ? error
            : L.S($"提交失败（HTTP {(int)resp.StatusCode}），请稍后重试。", $"Submit failed (HTTP {(int)resp.StatusCode}), please retry later."));
    }

    /// <summary>凭反馈码查询处理进度（服务端每 IP 每天 60 次）。NOT_FOUND / EXPIRED 属正常结果，不抛异常。</summary>
    public static async Task<QueryResult> QueryAsync(string code, CancellationToken ct = default)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"DLSS5Patcher/{Updater.CurrentVersion}");
        using var resp = await http.GetAsync(
            $"{PackageCatalog.ServerBase}/api/feedback/query/{Uri.EscapeDataString(code.Trim())}", ct);
        var text = await resp.Content.ReadAsStringAsync(ct);

        string? bodyCode = null, message = null;
        JsonElement data = default;
        var hasData = false;
        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (root.TryGetProperty("code", out var c) && c.TryGetInt32(out var ci)) bodyCode = ci.ToString();
            if (root.TryGetProperty("message", out var m)) message = m.GetString();
            if (root.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object) { data = d.Clone(); hasData = true; }
        }
        catch { }

        // 业务限流：HTTP 429 或信封 code=429
        if ((int)resp.StatusCode == 429 || bodyCode == "429")
            throw new InvalidOperationException(
                L.S("今日查询次数已达上限（每天 60 次），请明天再试。",
                    "Daily query limit reached (60/day). Please try again tomorrow."));

        if (!resp.IsSuccessStatusCode || bodyCode != "200" || !hasData)
            throw new InvalidOperationException(
                !string.IsNullOrEmpty(message) ? message
                : L.S($"查询失败（HTTP {(int)resp.StatusCode}），请稍后再试。", $"Query failed (HTTP {(int)resp.StatusCode}), please retry later."));

        return new QueryResult(
            data.TryGetProperty("statusCode", out var sc) ? sc.GetString() ?? "" : "",
            data.TryGetProperty("createdAt", out var ca) ? ca.GetString() ?? "" : "",
            data.TryGetProperty("username", out var un) ? un.GetString() ?? "" : "",
            data.TryGetProperty("adminReply", out var ar) ? ar.GetString() ?? "" : "");
    }

    private static byte[] BuildJson(Report r)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteString("app", "DLSS5Patcher");
            w.WriteString("version", r.Version);
            w.WriteString("os", r.Os);
            w.WriteString("runtime", r.Runtime);
            w.WriteStartObject("gpu");
            w.WriteString("name", r.Gpu.Name);
            w.WriteString("driver", r.Gpu.Driver);
            w.WriteString("vram", r.Gpu.VramText);
            w.WriteString("gen", r.Gpu.GenerationCn);
            w.WriteEndObject();

            w.WriteStartArray("games");
            foreach (var g in r.Games)
            {
                w.WriteStartObject();
                w.WriteString("name", g.Name);
                w.WriteString("dir", g.Dir);
                w.WriteString("source", g.Source);
                w.WriteString("state", g.State);
                w.WriteEndObject();
            }
            w.WriteEndArray();

            w.WriteString("description", r.Description);
            w.WriteString("username", r.Username ?? "");

            w.WriteStartArray("logs");
            foreach (var l in r.Logs)
            {
                w.WriteStartObject();
                w.WriteString("name", l.Name);
                w.WriteNumber("size", l.Size);
                w.WriteBoolean("truncated", l.Truncated);
                w.WriteString("content", l.Content);
                w.WriteEndObject();
            }
            w.WriteEndArray();

            w.WriteStartArray("screenshots");
            foreach (var s in r.Shots)
            {
                w.WriteStartObject();
                w.WriteString("name", s.Name);
                w.WriteNumber("size", s.Size);
                w.WriteString("data", Convert.ToBase64String(s.Data));
                w.WriteEndObject();
            }
            w.WriteEndArray();

            w.WriteEndObject();
        }
        return ms.ToArray();
    }
}
