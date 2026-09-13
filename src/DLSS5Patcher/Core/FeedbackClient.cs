using System.Text;
using System.Text.Json;

namespace DLSS5Patcher.Core;

/// <summary>反馈提交客户端：组装 JSON 载荷（环境 + 勾选游戏 + 日志 + 截图）并 POST 到分发服务器的反馈端点。</summary>
public static class FeedbackClient
{
    public sealed record GameLine(string Name, string Dir, string Source, string State);
    public sealed record LogEntry(string Name, long Size, bool Truncated, string Content);
    public sealed record ShotEntry(string Name, long Size, byte[] Data);
    public sealed record Report(
        string Version, string Os, string Runtime, GpuInfo Gpu,
        List<GameLine> Games, string Description,
        List<LogEntry> Logs, List<ShotEntry> Shots);

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

    public static async Task<string> SubmitAsync(Report report, CancellationToken ct = default)
    {
        var body = BuildJson(report);
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"DLSS5Patcher/{Updater.CurrentVersion}");
        using var content = new ByteArrayContent(body);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        using var resp = await http.PostAsync($"{PackageCatalog.ServerBase}/feedback", content, ct);
        var text = await resp.Content.ReadAsStringAsync(ct);
        string? id = null, error = null;
        try
        {
            using var doc = JsonDocument.Parse(text);
            id = doc.RootElement.TryGetProperty("id", out var i) ? i.GetString() : null;
            error = doc.RootElement.TryGetProperty("error", out var e) ? e.GetString() : null;
        }
        catch { }

        if (resp.IsSuccessStatusCode && !string.IsNullOrEmpty(id)) return id;
        throw new InvalidOperationException(
            !string.IsNullOrEmpty(error) ? error
            : L.S($"提交失败（HTTP {(int)resp.StatusCode}），请稍后重试。", $"Submit failed (HTTP {(int)resp.StatusCode}), please retry later."));
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
