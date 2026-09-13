using System.Security.Cryptography;
using System.Text.Json;

namespace DLSS5Patcher.Core;

/// <summary>
/// 加密赞助码客户端（接入方式见 MSFS_CAT_CH_SERVER sdk/sponsor-qr-client/README.md，同一套协议）：
/// 拉取 AES-256-GCM 信封 → 本地解密 → SHA-256 完整性校验 → 图片魔数校验。
/// 密钥在客户端与服务端各存一份、拆成两段硬编码（仅防直链热链的混淆；完整性由 GCM 认证保证，
/// 密文被篡改任何一字节都会解密失败，无法把别人的码换成自己的）。
/// </summary>
public static class SponsorQrClient
{
    private const int NonceBytes = 12;
    private const int AuthTagBytes = 16;
    private const int KeyBytes = 32;

    // 与服务端 qr-key 一致；拆两段存放（K_A + K_B 拼回 base64）
    private const string K_A = "ElS8r85Zt10yyxj3rJ+ndtLIcTx3Rkom";
    private const string K_B = "ABONi1AyUYU=";

    public sealed record QrResult(bool Ok, byte[]? Image, string Error);

    public static async Task<QrResult> FetchAsync(CancellationToken ct = default)
    {
        try
        {
            var key = Convert.FromBase64String(K_A + K_B);
            if (key.Length != KeyBytes) return new QrResult(false, null, "bad-key");

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"DLSS5Patcher/{Updater.CurrentVersion}");

            JsonElement body;
            try
            {
                using var resp = await http.GetAsync($"{PackageCatalog.ServerBase}/api/assets/sponsor-qr", ct);
                if (resp.StatusCode != System.Net.HttpStatusCode.OK)
                    return new QrResult(false, null, $"http-{(int)resp.StatusCode}");
                body = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct)).RootElement.Clone();
            }
            catch
            {
                return new QrResult(false, null, "network-error");
            }

            if (body.ValueKind != JsonValueKind.Object) return new QrResult(false, null, "bad-body");
            if (!TryGetString(body, "scheme", out var scheme) || scheme != "aes-256-gcm")
                return new QrResult(false, null, "scheme-unsupported");

            if (!TryGetBase64(body, "nonce", out var nonce) || nonce.Length != NonceBytes)
                return new QrResult(false, null, "bad-nonce");
            if (!TryGetBase64(body, "payload", out var payload) || payload.Length <= AuthTagBytes)
                return new QrResult(false, null, "bad-payload");
            if (!TryGetString(body, "sha256", out var shaHex)
                || shaHex.Length != 64 || !IsLowerHex(shaHex))
                return new QrResult(false, null, "bad-sha256");

            var authTag = payload[^AuthTagBytes..];
            var ciphertext = payload[..^AuthTagBytes];

            byte[] plaintext;
            try
            {
                using var aes = new AesGcm(key, AuthTagBytes);
                plaintext = new byte[ciphertext.Length];
                aes.Decrypt(nonce, ciphertext, authTag, plaintext);
            }
            catch
            {
                return new QrResult(false, null, "decrypt-failed");   // 密文被篡改或密钥错误
            }

            var digest = Convert.ToHexString(SHA256.HashData(plaintext)).ToLowerInvariant();
            if (digest != shaHex) return new QrResult(false, null, "sha256-mismatch");

            if (!IsSupportedImage(plaintext)) return new QrResult(false, null, "not-an-image");
            return new QrResult(true, plaintext, "");
        }
        catch
        {
            return new QrResult(false, null, "unexpected-error");
        }
    }

    private static bool TryGetString(JsonElement el, string name, out string value)
    {
        value = "";
        if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.String) return false;
        value = v.GetString() ?? "";
        return value.Length > 0;
    }

    private static bool TryGetBase64(JsonElement el, string name, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (!TryGetString(el, name, out var s) || s.Length % 4 != 0) return false;
        foreach (var ch in s)
        {
            var ok = char.IsLetterOrDigit(ch) || ch == '+' || ch == '/' || ch == '=';
            if (!ok) return false;
        }
        try { bytes = Convert.FromBase64String(s); return true; }
        catch { return false; }
    }

    private static bool IsLowerHex(string s)
    {
        foreach (var ch in s)
            if (!(ch >= '0' && ch <= '9') && !(ch >= 'a' && ch <= 'f')) return false;
        return true;
    }

    /// <summary>仅凭明文头部魔数判断图片格式，不信任服务器返回的任何 mime 描述。</summary>
    private static bool IsSupportedImage(byte[] b) =>
        b.Length >= 4
        && ((b[0] == 0xFF && b[1] == 0xD8)                                            // jpeg
            || (b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47));       // png
}
