using System.Text.Json;

namespace DLSS5Patcher.Core;

/// <summary>
/// 公告客户端：从分发服务器拉取已发布公告列表与弹窗公告。
/// 信封校验与 GSX 汉化一致：HTTP 200 + body.code == 200 + data 内为数组；
/// 非法条目逐条丢弃，不影响其他公告。
/// </summary>
public static class AnnouncementsClient
{
    public const int PageSize = 50;

    public sealed record Announcement(
        long Id, string Title, string Content, string? ImageUrl,
        bool Popup, bool Pinned, string Category, string CreatedByUsername, string CreatedAt);

    public sealed record FetchResult(bool Ok, List<Announcement> Announcements, string Error);

    /// <summary>公告类别展示名（global 全局 / software 软件 / patch:x 补丁）。</summary>
    public static string CategoryLabel(string category)
    {
        return category switch
        {
            "global" => L.S("全局", "Global"),
            "software" => L.S("软件", "Software"),
            _ => category.StartsWith("patch:")
                ? L.S("补丁 · ", "Patch · ") + category["patch:".Length..]
                : L.S("公告", "Notice"),
        };
    }

    /// <summary>时间展示：服务端 createdAt 按字符串原样截成 "yyyy-MM-dd HH:mm"，不做时区换算。</summary>
    public static string FormatTime(string value)
    {
        var m = System.Text.RegularExpressions.Regex.Match(value, @"^(\d{4})-(\d{2})-(\d{2})[T ](\d{2}):(\d{2})");
        return m.Success ? $"{m.Groups[1]}-{m.Groups[2]}-{m.Groups[3]} {m.Groups[4]}:{m.Groups[5]}" : value;
    }

    /// <summary>已发布公告列表（服务端已按 置顶优先 + createdAt 倒序 排序）。</summary>
    public static Task<FetchResult> FetchListAsync(CancellationToken ct = default) =>
        FetchEnvelopeAsync($"/api/announcements?page=0&size={PageSize}", body => PickContentArray(body), ct);

    /// <summary>弹窗公告列表（信封 data 直接是数组）。</summary>
    public static Task<FetchResult> FetchPopupAsync(CancellationToken ct = default) =>
        FetchEnvelopeAsync("/api/announcements/popup", body => PickDataArray(body), ct);

    private static JsonElement? PickContentArray(JsonElement root) =>
        root.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object
            && d.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.Array ? c : null;

    private static JsonElement? PickDataArray(JsonElement root) =>
        root.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Array ? d : null;

    private static async Task<FetchResult> FetchEnvelopeAsync(
        string path, Func<JsonElement, JsonElement?> pick, CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"DLSS5Patcher/{Updater.CurrentVersion}");

        JsonElement root;
        try
        {
            using var resp = await http.GetAsync($"{PackageCatalog.ServerBase}{path}", ct);
            if (resp.StatusCode != System.Net.HttpStatusCode.OK)
                return new FetchResult(false, new List<Announcement>(), L.S($"公告获取失败（HTTP {(int)resp.StatusCode}），请稍后重试", $"Failed to fetch announcements (HTTP {(int)resp.StatusCode}), retry later"));
            root = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct)).RootElement.Clone();
        }
        catch
        {
            return new FetchResult(false, new List<Announcement>(),
                L.S("暂时无法连接公告服务器，请检查网络后重试", "Cannot reach the announcement server, check your network and retry"));
        }

        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("code", out var code) || code.ValueKind != JsonValueKind.Number || code.GetInt32() != 200)
            return new FetchResult(false, new List<Announcement>(),
                L.S("公告接口返回异常，请稍后重试", "Announcement API error, retry later"));

        var arr = pick(root);
        if (arr is not { ValueKind: JsonValueKind.Array })
            return new FetchResult(false, new List<Announcement>(),
                L.S("公告数据格式无效，请稍后重试", "Invalid announcement data, retry later"));

        var list = new List<Announcement>();
        foreach (var el in arr.Value.EnumerateArray())
        {
            var a = Normalize(el);
            if (a != null) list.Add(a);
        }
        return new FetchResult(true, list, "");
    }

    private static Announcement? Normalize(JsonElement el)
    {
        try
        {
            if (el.ValueKind != JsonValueKind.Object) return null;
            long id = el.GetProperty("id").GetInt64();
            string title = el.GetProperty("title").GetString() ?? "";
            string content = el.GetProperty("content").GetString() ?? "";
            string category = el.GetProperty("category").GetString() ?? "";
            if (id <= 0 || title.Trim().Length == 0 || content.Trim().Length == 0 || !IsValidCategory(category)) return null;
            return new Announcement(
                id, title, content,
                el.TryGetProperty("imageUrl", out var img) && img.ValueKind == JsonValueKind.String ? img.GetString() : null,
                el.TryGetProperty("popup", out var p) && p.ValueKind == JsonValueKind.True,
                el.TryGetProperty("pinned", out var pin) && pin.ValueKind == JsonValueKind.True,
                category,
                el.TryGetProperty("createdByUsername", out var u) && u.ValueKind == JsonValueKind.String ? u.GetString() ?? "" : "",
                el.TryGetProperty("createdAt", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() ?? "" : "");
        }
        catch { return null; }
    }

    private static bool IsValidCategory(string v) =>
        v is "global" or "software" || (v.StartsWith("patch:") && v.Length > "patch:".Length);
}
